using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
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
        private static readonly string[] BuiltInBuildNumbers = { "lastSuccessfulBuild", "lastStableBuild", "lastBuild", "lastCompletedBuild" };

        private IJenkinsConnectionInfo config;
        private readonly ILogSink logger;
        private readonly CancellationToken cancellationToken;

        public JenkinsClient(IJenkinsConnectionInfo config, ILogSink logger, CancellationToken cancellationToken)
        {
            this.config = config;
            this.logger = logger;
            this.cancellationToken = cancellationToken;
        }

        private async Task<HttpClient> CreateHttpClientAsync()
        {
            var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };

            if (!string.IsNullOrEmpty(config.UserName))
            {
                this.logger?.LogDebug("Setting Authorization request header...");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(InedoLib.UTF8Encoding.GetBytes(config.UserName + ":" + config.Password)));
            }

            if (this.config.CsrfProtectionEnabled)
            {
                this.logger?.LogDebug("Checking for CSRF protection...");
                using (var response = await client.GetAsync(this.config.GetApiUrl() + "/crumbIssuer/api/xml?xpath=concat(//crumbRequestField,\":\",//crumb)").ConfigureAwait(false))
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

        private async Task<string> GetAsync(string url)
        {
            if (string.IsNullOrEmpty(this.config.ServerUrl))
                return null;

            using (var client = await this.CreateHttpClientAsync().ConfigureAwait(false))
            {
                var downloadUrl = this.config.GetApiUrl() + '/' + url.TrimStart('/');
                this.logger?.LogDebug($"Downloading string from {downloadUrl}...");
                using (var response = await client.GetAsync(downloadUrl, this.cancellationToken).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
            }
        }
        private async Task<string> PostAsync(string url)
        {
            if (string.IsNullOrEmpty(this.config.ServerUrl))
                throw new InvalidOperationException("Jenkins ServerUrl has not been set.");

            using (var client = await this.CreateHttpClientAsync().ConfigureAwait(false))
            {
                var uploafUrl = this.config.GetApiUrl() + '/' + url.TrimStart('/');
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
            if (string.IsNullOrEmpty(this.config.ServerUrl))
                throw new InvalidOperationException("Jenkins ServerUrl has not been set.");

            using (var client = await this.CreateHttpClientAsync().ConfigureAwait(false))
            {
                var downloadUrl = this.config.GetApiUrl() + '/' + url.TrimStart('/');
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
            if (string.IsNullOrEmpty(this.config.ServerUrl))
                throw new InvalidOperationException("Jenkins ServerUrl has not been set.");

            var client = await this.CreateHttpClientAsync().ConfigureAwait(false);
            
            var downloadUrl = this.config.GetApiUrl() + '/' + url.TrimStart('/');
            this.logger?.LogDebug($"Downloading file from {downloadUrl}...");
            var response = await client.GetAsync(downloadUrl, this.cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return new OpenArtifact(client, response, await response.Content.ReadAsStreamAsync().ConfigureAwait(false));
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

        public async Task<string> GetSpecialBuildNumberAsync(string jobName, string buildNumber)
        {
            string result = await this.GetAsync("job/" + Uri.EscapeUriString(jobName) + "/api/xml").ConfigureAwait(false);
            return XDocument.Parse(result)
                .Descendants(buildNumber)
                .Select(n => n.Element("number").Value)
                .FirstOrDefault();
        }

        public async Task<List<string>> GetBuildNumbersAsync(string jobName)
        {
            string result = await this.GetAsync("job/" + Uri.EscapeUriString(jobName) + "/api/xml?xpath=/freeStyleProject/build/number&wrapper=builds").ConfigureAwait(false);
            var results = XDocument.Parse(result)
                .Descendants("number")
                .Select(n => n.Value)
                .Where(s => !string.IsNullOrEmpty(s));

            return BuiltInBuildNumbers.Concat(results).ToList();
        }

        public Task DownloadArtifactAsync(string jobName, string buildNumber, string fileName)
        {
            return this.DownloadAsync(
                "/job/" + Uri.EscapeUriString(jobName) + '/' + Uri.EscapeUriString(buildNumber) + "/artifact/*zip*/archive.zip",
                fileName);
        }

        public Task<OpenArtifact> OpenArtifactAsync(string jobName, string buildNumber)
        {
            return this.OpenAsync("/job/" + Uri.EscapeUriString(jobName) + '/' + Uri.EscapeUriString(buildNumber) + "/artifact/*zip*/archive.zip");
        }

        public async Task<List<JenkinsBuildArtifact>> GetBuildArtifactsAsync(string jobName, string buildNumber)
        {
            string result = await this.GetAsync("job/" + Uri.EscapeUriString(jobName) + "/" + Uri.EscapeUriString(buildNumber) + "/api/xml").ConfigureAwait(false);
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

        public Task DownloadSingleArtifactAsync(string jobName, string buildNumber, string fileName, JenkinsBuildArtifact artifact)
        {
            return this.DownloadAsync(
                "/job/" + Uri.EscapeUriString(jobName) + '/' + Uri.EscapeUriString(buildNumber) + "/artifact/" + artifact.RelativePath,
                fileName);
        }

        public Task<OpenArtifact> OpenSingleArtifactAsync(string jobName, string buildNumber, JenkinsBuildArtifact artifact)
        {
            return this.OpenAsync("/job/" + Uri.EscapeUriString(jobName) + '/' + Uri.EscapeUriString(buildNumber) + "/artifact/" + artifact.RelativePath);
        }

        public async Task<int> TriggerBuildAsync(string jobName, string additionalParameters = null)
        {
            var url = "/job/" + Uri.EscapeUriString(jobName) + "/build";
            if (!string.IsNullOrEmpty(additionalParameters))
                url += "WithParameters?" + additionalParameters;
            return int.Parse(PathEx.GetFileName(await this.PostAsync(url).ConfigureAwait(false)));
        }

        public async Task<JenkinsQueueItem> GetQueuedBuildInfoAsync(int queueItem)
        {
            using (var client = await this.CreateHttpClientAsync().ConfigureAwait(false))
            using (var response = await client.GetAsync(this.config.GetApiUrl() + "/queue/item/" + queueItem + "/api/xml?tree=executable[number],why", this.cancellationToken).ConfigureAwait(false))
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

        public async Task<JenkinsBuild> GetBuildInfoAsync(string jobName, string buildNumber)
        {
            using (var client = await this.CreateHttpClientAsync().ConfigureAwait(false))
            using (var response = await client.GetAsync(this.config.GetApiUrl()
                + "/job/" + Uri.EscapeUriString(jobName) + '/' + Uri.EscapeUriString(buildNumber)
                + "/api/xml?tree=building,result,number,duration,estimatedDuration", this.cancellationToken).ConfigureAwait(false))
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                    return null;
                response.EnsureSuccessStatusCode();

                var build = XDocument.Load(await response.Content.ReadAsStreamAsync().ConfigureAwait(false)).Root;
                return new JenkinsBuild
                {
                    Building = (bool)build.Element("building"),
                    Result = build.Elements("result").Select(e => e.Value).FirstOrDefault(),
                    Number = build.Elements("number").Select(e => e.Value).FirstOrDefault(),
                    Duration = build.Elements("duration").Select(e => AH.ParseInt(e.Value)).FirstOrDefault(),
                    EstimatedDuration = build.Elements("estimatedDuration").Select(e => AH.ParseInt(e.Value)).FirstOrDefault()
                };
            }
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
