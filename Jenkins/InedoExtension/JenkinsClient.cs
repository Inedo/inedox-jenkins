using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Inedo.Diagnostics;
using Inedo.IO;

[assembly: InternalsVisibleTo("InedoExtensionTests")]
namespace Inedo.Extensions.Jenkins
{
    internal sealed class JenkinsClient
    {
        private enum NotFoundAction
        {
            ThrowException, ReturnNull
        }

        private static readonly string[] BuiltInBuildNumbers = { "lastSuccessfulBuild", "lastStableBuild", "lastBuild", "lastCompletedBuild" };

        private readonly ILogSink logger;
        private readonly CancellationToken cancellationToken;
        private readonly string username;
        private readonly SecureString password;
        private readonly string serverUrl;
        private readonly bool csrfProtectionEnabled;

        public JenkinsClient(string username, SecureString password, string serverUrl,  bool csrfProtectionEnabled, ILogSink logger = null, CancellationToken cancellationToken = default)
        {
            this.username = username;
            this.password = password;
            this.serverUrl = serverUrl;
            this.csrfProtectionEnabled = csrfProtectionEnabled;
            this.logger = logger;
            this.cancellationToken = cancellationToken;
        }

        private async Task<HttpClient> CreateHttpClientAsync()
        {
            var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };

            if (!string.IsNullOrEmpty(this.username))
            {
                this.logger?.LogDebug("Setting Authorization request header...");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(InedoLib.UTF8Encoding.GetBytes(this.username + ":" + AH.Unprotect(this.password))));
            }

            if (this.csrfProtectionEnabled)
            {
                this.logger?.LogDebug("Checking for CSRF protection...");
                using (var response = await client.GetAsync(IJenkinsConnectionInfoExtensions.GetApiUrl(this.serverUrl) + "/crumbIssuer/api/xml?xpath=concat(//crumbRequestField,\":\",//crumb)").ConfigureAwait(false))
                {
                    // Assume if the request failed that Jenkins is not set up to use CSRF protection.
                    if (response.IsSuccessStatusCode)
                    {
                        var csrfHeader = (await response.Content.ReadAsStringAsync().ConfigureAwait(false)).Split(new[] { ':' }, 2);
                        if (csrfHeader.Length == 2)
                        {
                            client.DefaultRequestHeaders.Add(csrfHeader[0], csrfHeader[1]);
                        }
                    }
                }
            }

            return client;
        }

        private async Task<string> GetAsync(string url, NotFoundAction action = NotFoundAction.ThrowException)
        {
            if (string.IsNullOrEmpty(serverUrl))
                return null;

            using (var client = await this.CreateHttpClientAsync().ConfigureAwait(false))
            {
                var downloadUrl = IJenkinsConnectionInfoExtensions.GetApiUrl(this.serverUrl) + '/' + url.TrimStart('/');
                this.logger?.LogDebug($"Downloading string from {downloadUrl}...");
                using (var response = await client.GetAsync(downloadUrl, this.cancellationToken).ConfigureAwait(false))
                {
                    if (response.StatusCode == HttpStatusCode.NotFound && action == NotFoundAction.ReturnNull)
                        return null;

                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
            }
        }

        private async Task<string> PostAsync(string url)
        {
            if (string.IsNullOrEmpty(serverUrl))
                throw new InvalidOperationException("Jenkins ServerUrl has not been set.");

            using (var client = await this.CreateHttpClientAsync().ConfigureAwait(false))
            {
                var uploafUrl = IJenkinsConnectionInfoExtensions.GetApiUrl(this.serverUrl) + '/' + url.TrimStart('/');
                this.logger?.LogDebug($"Posting to {uploafUrl}...");
                using (var response = await client.PostAsync(uploafUrl, new StringContent(string.Empty), this.cancellationToken).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        string message = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        throw new WebException("Invalid Jenkins API call, response body was: " + message);
                    }
                    return response.Headers.Location?.OriginalString;
                }
            }
        }
        private async Task DownloadAsync(string url, string toFileName)
        {
            if (string.IsNullOrEmpty(serverUrl))
                throw new InvalidOperationException("Jenkins ServerUrl has not been set.");

            using (var client = await this.CreateHttpClientAsync().ConfigureAwait(false))
            {
                var downloadUrl = IJenkinsConnectionInfoExtensions.GetApiUrl(this.serverUrl) + '/' + url.TrimStart('/');
                this.logger?.LogDebug($"Downloading file from {downloadUrl}...");
                using (var response = await client.GetAsync(downloadUrl, this.cancellationToken).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    using (var file = new FileStream(toFileName, FileMode.Create, FileAccess.Write))
                    {
                        await response.Content.CopyToAsync(file).ConfigureAwait(false);
                    }
                }
            }
        }

        private async Task<OpenArtifact> OpenAsync(string url)
        {
            if (string.IsNullOrEmpty(this.serverUrl))
                throw new InvalidOperationException("Jenkins ServerUrl has not been set.");

            var client = await this.CreateHttpClientAsync().ConfigureAwait(false);
            
            var downloadUrl = IJenkinsConnectionInfoExtensions.GetApiUrl(this.serverUrl) + '/' + url.TrimStart('/');
            this.logger?.LogDebug($"Downloading file from {downloadUrl}...");
            var response = await client.GetAsync(downloadUrl, this.cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return new OpenArtifact(client, response, await response.Content.ReadAsStreamAsync().ConfigureAwait(false));
        }

        private string GetXmlApiUrl(string jobName, string branchName, string buildNumber, string queryString)
        {
            string url = GetPath(jobName, branchName, buildNumber);

            url += "/api/xml";

            if (!String.IsNullOrEmpty(queryString))
            {
                url += $"?{queryString}";
            }

            return url;
        }

        private string GetApiUrl(string jobName, string branchName, string buildNumber, string path)
        {
            string url = GetPath(jobName, branchName, buildNumber);

            if (!String.IsNullOrEmpty(path))
                url += $"/{path}";
            
            return url;
        }

        public static string GetPath(string jobName, string branchName = null, string buildNumber = null)
        {
            string path = $"job/{Uri.EscapeUriString(jobName)}";

            if (!String.IsNullOrEmpty(branchName))
                path += $"/job/{Uri.EscapeUriString(branchName)}";

            if (!String.IsNullOrEmpty(buildNumber))
                path += $"/{buildNumber}";

            return path;
        }

        public async Task<string[]> GetJobNamesAsync()
        {
            var xml = await this.GetAsync("api/xml?tree=jobs[name]").ConfigureAwait(false);
            if (xml == null)
                return new string[0];

            return XDocument.Parse(xml)
                .Descendants("name")
                .Select(n => n.Value)
                .ToArray();
        }

        public async Task<string> GetSpecialBuildNumberAsync(string jobName, string branchName, string specialBuildNumber)
        {
            string result = await this.GetAsync(GetXmlApiUrl(jobName, branchName, null, $"tree={specialBuildNumber}[number]")).ConfigureAwait(false);

            return XDocument.Parse(result)
                .Descendants("number")
                .Select(n => n.Value)
                .FirstOrDefault();
        }

        public async Task<List<string>> GetBuildNumbersAsync(string jobName, string branchName)
        {
            string result = await this.GetAsync(GetXmlApiUrl(jobName, branchName, null, "tree=builds[number]")).ConfigureAwait(false);
            var results = XDocument.Parse(result)
                .Descendants("number")
                .Select(n => n.Value)
                .Where(s => !string.IsNullOrEmpty(s));

            if (results.Count() == 0 && String.IsNullOrEmpty(branchName) && XDocument.Parse(result).Root.Name.LocalName.Equals("workflowMultiBranchProject"))
            {
                throw new InvalidOperationException("branchName parameter is required to retrieve builds for a Multi-Branch project");
            }

            return BuiltInBuildNumbers.Concat(results).ToList();
        }

        public async Task<List<string>> GetBranchNamesAsync(string jobName)
        {
            string result = await this.GetAsync(GetXmlApiUrl(jobName, null, null, "tree=jobs[name]")).ConfigureAwait(false);
            return XDocument.Parse(result)
                .Descendants("name")
                .Select(n => n.Value)
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }
        
        public async Task<JenkinsBuild> GetBuildInfoAsync(string jobName, string branchName, string buildNumber)
        {
            var xml = await this.GetAsync(GetXmlApiUrl(jobName, branchName, buildNumber, "tree=building,result,number,duration,estimatedDuration"), NotFoundAction.ReturnNull).ConfigureAwait(false);

            if (xml == null)
                return null;

            var build = XDocument.Parse(xml).Root;

            return new JenkinsBuild
            {
                Building = (bool)build.Element("building"),
                Result = (string)build.Element("result"),
                Number = (string)build.Element("number"),
                Duration = (int)build.Element("duration"),
                EstimatedDuration = (int)build.Element("estimatedDuration")
            };
        }

        public async Task<List<JenkinsBuildArtifact>> GetBuildArtifactsAsync(string jobName, string branchName, string buildNumber)
        {
            string result = await this.GetAsync(GetXmlApiUrl(jobName, branchName, buildNumber, "tree=artifacts[*]")).ConfigureAwait(false);
            return XDocument.Parse(result)
                .Descendants("artifact")
                .Select(n => new JenkinsBuildArtifact
                {
                    DisplayPath = (string)n.Element("displayPath"),
                    FileName = (string)n.Element("fileName"),
                    RelativePath = (string)n.Element("relativePath")
                })
                .ToList();
        }

        public async Task<JenkinsQueueItem> GetQueuedBuildInfoAsync(int queueItem)
        {
            using (var client = await this.CreateHttpClientAsync().ConfigureAwait(false))
            using (var response = await client.GetAsync(IJenkinsConnectionInfoExtensions.GetApiUrl(this.serverUrl) + "/queue/item/" + queueItem + "/api/xml?tree=executable[number],why", this.cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();

                var item = XDocument.Load(await response.Content.ReadAsStreamAsync().ConfigureAwait(false)).Root;

                var buildNumber = item.Element("executable")?.Element("number")?.Value;
                if (!string.IsNullOrEmpty(buildNumber))
                {
                    return new JenkinsQueueItem
                    {
                        BuildNumber = buildNumber
                    };
                }

                return new JenkinsQueueItem
                {
                    WaitReason = item.Element("why")?.Value
                };
            }
        }

        public Task DownloadArtifactAsync(string jobName, string branchName, string buildNumber, string fileName)
        {
            return this.DownloadAsync(GetApiUrl(jobName, branchName, buildNumber, "artifact/*zip*/archive.zip"), fileName);
        }

        public Task DownloadSingleArtifactAsync(string jobName, string branchName, string buildNumber, string fileName, JenkinsBuildArtifact artifact)
        {
            return this.DownloadAsync(GetApiUrl(jobName, branchName, buildNumber, $"artifact/{artifact.RelativePath}"), fileName);
        }

        public Task<OpenArtifact> OpenArtifactAsync(string jobName, string branchName, string buildNumber)
        {
            return this.OpenAsync(GetApiUrl(jobName, branchName, buildNumber, "artifact/*zip*/archive.zip"));
        }

        public Task<OpenArtifact> OpenSingleArtifactAsync(string jobName, string branchName, string buildNumber, JenkinsBuildArtifact artifact)
        {
            return this.OpenAsync(GetApiUrl(jobName, branchName, buildNumber, $"artifact/{artifact.RelativePath}"));
        }

        public async Task<int> TriggerBuildAsync(string jobName, string branchName, string additionalParameters = null)
        {
            var url = GetApiUrl(jobName, branchName, null, "build");            
            if (!string.IsNullOrEmpty(additionalParameters))
                url += "WithParameters?" + additionalParameters;
            return int.Parse(PathEx.GetFileName(await this.PostAsync(url).ConfigureAwait(false)));
        }
    }

    [Serializable]
    internal sealed class JenkinsQueueItem
    {
        public string BuildNumber { get; set; }
        public string WaitReason { get; set; }
    }

    [Serializable]
    internal sealed class JenkinsBuild
    {
        public bool Building { get; set; }
        public string Number { get; set; }
        public string Result { get; set; }
        public int? Duration { get; set; }
        public int? EstimatedDuration { get; set; }
    }

    [Serializable]
    internal sealed class JenkinsBuildArtifact
    {
        public string DisplayPath { get; set; }
        public string FileName { get; set; }
        public string RelativePath { get; set; }
    }

    internal sealed class OpenArtifact : IDisposable
    {
        private readonly HttpClient client;
        private readonly HttpResponseMessage response;

        public OpenArtifact(HttpClient client, HttpResponseMessage response, Stream content)
        {
            this.client = client;
            this.response = response;
            this.Content = content;
        }

        public Stream Content { get; }

        public void Dispose()
        {
            this.Content?.Dispose();
            this.response?.Dispose();
            this.client?.Dispose();
        }
    }
}
