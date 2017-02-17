using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Xml.Linq;
using Inedo.Diagnostics;

namespace Inedo.Extensions.Jenkins
{
    internal sealed class JenkinsClient
    {
        private static readonly string[] BuiltInBuildNumbers = { "lastSuccessfulBuild", "lastStableBuild", "lastBuild", "lastCompletedBuild" };

        private IJenkinsConnectionInfo config;
        private ILogger logger;

        public JenkinsClient(IJenkinsConnectionInfo config, ILogger logger = null)
        {
            this.config = config;
            this.logger = logger;
        }

        private WebClient CreateWebClient()
        {
            var wc = new WebClient();
            if (!string.IsNullOrEmpty(config.UserName))
            {
                this.logger?.LogDebug($"Creating WebClient with username {config.UserName}...");
                wc.Headers[HttpRequestHeader.Authorization] = "Basic " + Convert.ToBase64String(InedoLib.UTF8Encoding.GetBytes(config.UserName + ":" + config.Password));
            }

            return wc;
        }

        private async Task<string> GetAsync(string url)
        {
            if (string.IsNullOrEmpty(this.config.ServerUrl)) return null;

            using (var wc = this.CreateWebClient())
            {
                var downloafUrl = this.config.GetApiUrl() + '/' + url.TrimStart('/');
                this.logger?.LogDebug($"Downloading string from {downloafUrl}...");
                return await wc.DownloadStringTaskAsync(downloafUrl).ConfigureAwait(false);
            }
        }
        private async Task PostAsync(string url)
        {
            if (string.IsNullOrEmpty(this.config.ServerUrl))
                throw new InvalidOperationException("Jenkins ServerUrl has not been set.");

            using (var wc = this.CreateWebClient())
            {
                var uploafUrl = this.config.GetApiUrl() + '/' + url.TrimStart('/');
                this.logger?.LogDebug($"Posting to {uploafUrl}...");
                try
                {
                    await wc.UploadStringTaskAsync(uploafUrl, string.Empty).ConfigureAwait(false);
                }
                catch (WebException wex) when (wex.Response != null)
                {
                    using (var stream = wex.Response.GetResponseStream())
                    {
                        string message = await new StreamReader(stream).ReadToEndAsync().ConfigureAwait(false);
                        throw new WebException("Invalid Jenkins API call, response body was: " + message, wex, wex.Status, wex.Response);
                    }
                }
            }
        }
        private async Task DownloadAsync(string url, string toFileName)
        {
            if (string.IsNullOrEmpty(this.config.ServerUrl))
                throw new InvalidOperationException("Jenkins ServerUrl has not been set.");

            using (var wc = this.CreateWebClient())
            {
                var downloafUrl = this.config.GetApiUrl() + '/' + url.TrimStart('/');
                this.logger?.LogDebug($"Downloading file from {downloafUrl}...");
                await wc.DownloadFileTaskAsync(downloafUrl, toFileName).ConfigureAwait(false);
            }
        }

        private async Task<OpenArtifact> OpenAsync(string url)
        {
            if (string.IsNullOrEmpty(this.config.ServerUrl))
                throw new InvalidOperationException("Jenkins ServerUrl has not been set.");

            var wc = this.CreateWebClient();
            
            var downloafUrl = this.config.GetApiUrl() + '/' + url.TrimStart('/');
            this.logger?.LogDebug($"Downloading file from {downloafUrl}...");
            var content = await wc.OpenReadTaskAsync(downloafUrl).ConfigureAwait(false);
            return new OpenArtifact(wc, content);
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

        public async Task TriggerBuildAsync(string jobName, string additionalParameters = null)
        {
            var url = "/job/" + Uri.EscapeUriString(jobName) + "/build";
            if (!string.IsNullOrEmpty(additionalParameters))
                url += "WithParameters?" + additionalParameters;
            await this.PostAsync(url).ConfigureAwait(false);
        }

        public async Task<string> GetNextBuildNumberAsync(string jobName)
        {
            string result = await this.GetAsync("/job/" + Uri.EscapeUriString(jobName) + "/api/xml?tree=nextBuildNumber").ConfigureAwait(false);
            return XDocument.Parse(result)
                .Descendants("nextBuildNumber")
                .Select(n => n.Value)
                .FirstOrDefault();
        }

        public async Task<JenkinsBuild> GetBuildInfoAsync(string jobName, string buildNumber)
        {
            try
            {
                string result = await this.GetAsync(
                    "/job/" + Uri.EscapeUriString(jobName) + '/' + Uri.EscapeUriString(buildNumber)
                    + "/api/xml?tree=building,result,number").ConfigureAwait(false);
                var n = XDocument.Parse(result).Root;
                return new JenkinsBuild
                {
                    Building = (bool)n.Element("building"),
                    Result = n.Elements("result").Select(e => e.Value).FirstOrDefault(),
                    Number = n.Elements("number").Select(e => e.Value).FirstOrDefault(),
                    Duration = n.Elements("duration").Select(e => AH.ParseInt(e.Value)).FirstOrDefault(),
                    EstimatedDuration = n.Elements("estimatedDuration").Select(e => AH.ParseInt(e.Value)).FirstOrDefault()
                };
            }
            catch (WebException wex)
            {
                var status = wex.Response as HttpWebResponse;
                if (status != null && status.StatusCode == HttpStatusCode.NotFound) return null;

                throw;
            }
        }
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
        private WebClient client;

        public OpenArtifact(WebClient client, Stream content)
        {
            this.client = client;
            this.Content = content;
        }

        public Stream Content { get; }

        public void Dispose()
        {
            this.client?.Dispose();
            this.Content?.Dispose();
        }
    }
}
