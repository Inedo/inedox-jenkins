using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Xml.Linq;
using Inedo.Diagnostics;
using Inedo.Extensions.Jenkins.Extensions;
using Inedo.IO;

namespace Inedo.Extensions.Jenkins
{
    internal sealed class JenkinsClient
    {
        private static readonly string[] BuiltInBuildNumbers = { "lastSuccessfulBuild", "lastStableBuild", "lastBuild", "lastCompletedBuild" };
        private const string defaultArtifactPath = "/artifact/*zip*/archive.zip";

        private IJenkinsConnectionInfo config;
        private ILogSink logger;

        public JenkinsClient(IJenkinsConnectionInfo config, ILogSink logger = null)
        {
            this.config = config;
            this.logger = logger;
        }

        private static class ErrorMessages
        {
            public const string MissingServerUrl = "Jenkins Server Url has not been set.";
        }

        private async Task<HttpClient> CreateHttpClientAsync()
        {
            var client = new HttpClient();
            if (!string.IsNullOrEmpty(config.UserName))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(InedoLib.UTF8Encoding.GetBytes($"{config.UserName}:{config.Password}")));
            }

            if (config.CsrfProtectionEnabled)
            {
                using (var response = await client.GetAsync($"{config.GetApiUrl()}/crumbIssuer/api/xml?xpath=concat(//crumbRequestField,\":\",//crumb)").ConfigureAwait(false))
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
            if (string.IsNullOrEmpty(config.ServerUrl)) return null;

            using (var client = await CreateHttpClientAsync().ConfigureAwait(false))
            {
                var downloadUrl = Url(url);
                logger?.LogDebug($"Downloading string from {downloadUrl}...");
                using (var response = await client.GetAsync(downloadUrl).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
            }
        }

        private async Task<string> PostAsync(string url)
        {
            ValidateJenkinsServerUrl();

            using (var client = await CreateHttpClientAsync().ConfigureAwait(false))
            {
                var uploadUrl = Url(url);
                logger?.LogDebug($"Posting to {uploadUrl}...");
                using (var response = await client.PostAsync(uploadUrl, new StringContent(string.Empty)).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        string message = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        throw new WebException($"Invalid Jenkins API call, response body was: {message}");
                    }
                    return response.Headers.Location?.OriginalString;
                }
            }
        }
        private async Task DownloadAsync(string url, string toFileName)
        {
            ValidateJenkinsServerUrl();

            using (var client = await CreateHttpClientAsync().ConfigureAwait(false))
            {
                var downloadUrl = Url(url);
                logger?.LogDebug($"Downloading file from {downloadUrl}...");
                using (var response = await client.GetAsync(downloadUrl).ConfigureAwait(false))
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
            ValidateJenkinsServerUrl();

            var client = await CreateHttpClientAsync().ConfigureAwait(false);

            var downloadUrl = Url(url);
            logger?.LogDebug($"Downloading file from {downloadUrl}...");
            var response = await client.GetAsync(downloadUrl).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return new OpenArtifact(client, response, await response.Content.ReadAsStreamAsync().ConfigureAwait(false));
        }

        public async Task<string[]> GetJobNamesAsync()
        {
            var xml = await GetAsync("api/xml?tree=jobs[name]").ConfigureAwait(false);
            if (xml == null)
                return new string[0];

            return XDocument.Parse(xml)
                .Descendants("name")
                .Select(n => n.Value)
                .ToArray();
        }

        public async Task<string> GetSpecialBuildNumberAsync(string jobName, string buildNumber)
        {
            string result = await GetAsync($"{jobXPath(jobName)}/api/xml").ConfigureAwait(false);
            return XDocument.Parse(result)
                .Descendants(buildNumber)
                .Select(n => n.Element("number").Value)
                .FirstOrDefault();
        }

        public async Task<List<string>> GetBuildNumbersAsync(string jobName)
        {
            string result = await GetAsync($"{jobXPath(jobName)}/api/xml?xpath=/freeStyleProject/build/number&wrapper=builds").ConfigureAwait(false);
            var results = XDocument.Parse(result)
                .Descendants("number")
                .Select(n => n.Value)
                .Where(s => !string.IsNullOrEmpty(s));

            return BuiltInBuildNumbers.Concat(results).ToList();
        }

        public Task DownloadArtifactAsync(string jobName, string buildNumber, string fileName, string archivePath = null) => DownloadAsync(ArtifactPath(jobName, buildNumber, archivePath), fileName);

        public Task<OpenArtifact> OpenArtifactAsync(string jobName, string buildNumber, string archivePath = null) => OpenAsync(ArtifactPath(jobName, buildNumber, archivePath));

        public async Task<List<JenkinsBuildArtifact>> GetBuildArtifactsAsync(string jobName, string buildNumber)
        {
            string result = await GetAsync($"{buildXPath(jobName, buildNumber)}/api/xml").ConfigureAwait(false);
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

        public Task DownloadSingleArtifactAsync(string jobName, string buildNumber, string fileName, JenkinsBuildArtifact artifact) => DownloadAsync($"{ArtifactPath(jobName, buildNumber, "/artifact/{artifact.RelativePath}")}", fileName);

        public Task<OpenArtifact> OpenSingleArtifactAsync(string jobName, string buildNumber, JenkinsBuildArtifact artifact) => OpenAsync($"{ArtifactPath(jobName, buildNumber, "/artifact/{artifact.RelativePath}")}");

        public async Task<int> TriggerBuildAsync(string jobName, string additionalParameters = null)
        {
            var url = $"{JobPath(jobName)}/build";
            if (!string.IsNullOrEmpty(additionalParameters))
                url += $"WithParameters?{additionalParameters}";
            return int.Parse(PathEx.GetFileName(await PostAsync(url).ConfigureAwait(false)));
        }

        public async Task<JenkinsQueueItem> GetQueuedBuildInfoAsync(int queueItem)
        {
            using (var client = await CreateHttpClientAsync().ConfigureAwait(false))
            using (var response = await client.GetAsync($"{config.GetApiUrl()}/queue/item/{queueItem}/api/xml?tree=executable[number],why").ConfigureAwait(false))
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
            using (var client = await CreateHttpClientAsync().ConfigureAwait(false))
            using (var response = await client.GetAsync($"{config.GetApiUrl()}{BuildPath(jobName, buildNumber)}/api/xml?tree=building,result,number,duration,estimatedDuration").ConfigureAwait(false))
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

        private static string ArtifactPath(string jobName, string buildNumber, string archivePath = null) => $"{BuildPath(jobName, buildNumber)}{ArchivePath(archivePath)}";

        private static string BuildPath(string jobName, string buildNumber) => $"{JobPath(jobName)}/{Uri.EscapeUriString(buildNumber)}";

        private static string JobPath(string jobName) => $"/job/{Uri.EscapeUriString(jobName)}";

        private static string BuildUrl(string apiUrl, string jobName, string buildNumber) => $"{apiUrl}{BuildPath(jobName, buildNumber)}";

        private static string buildXPath(string jobName, string buildNumber) => $"{jobXPath(jobName)}/{Uri.EscapeUriString(buildNumber)}";

        private static string jobXPath(string jobName) => $"job/{Uri.EscapeUriString(jobName)}";

        private static string ArchivePath(string path = null) => string.IsNullOrWhiteSpace(path) ? defaultArtifactPath : string.Format("/artifact/{0}/*zip*/{1}.zip", path.TrimEndsCharacter('/'), LastFolder(path));

        private static string LastFolder(string path) => string.IsNullOrWhiteSpace(path) ? path : new DirectoryInfo(path).Name;

        private string Url(string url) => $"{config.GetApiUrl()}/{url.TrimStart('/')}";
        private void ValidateJenkinsServerUrl()
        {
            if (string.IsNullOrEmpty(config.ServerUrl)) throw new InvalidOperationException(ErrorMessages.MissingServerUrl);
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
            Content = content;
        }

        public Stream Content { get; }

        public void Dispose()
        {
            Content?.Dispose();
            response?.Dispose();
            client?.Dispose();
        }
    }
}
