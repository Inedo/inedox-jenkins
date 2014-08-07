using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Xml.Linq;

namespace Inedo.BuildMasterExtensions.Jenkins
{
    internal sealed class JenkinsClient
    {
        JenkinsConfigurer config;

        public JenkinsClient(JenkinsConfigurer config)
        {
            this.config = config;
        }

        private WebClient CreateWebClient()
        {
            var wc = new WebClient();
            if (!string.IsNullOrEmpty(config.Username))
                wc.Credentials = new NetworkCredential(config.Username, config.Password);

            return wc;
        }

        private string Get(string url)
        {
            if (string.IsNullOrEmpty(this.config.ServerUrl))
                throw new InvalidOperationException("Jenkins ServerUrl has not been set.");

            using (var wc = this.CreateWebClient())
            {
                return wc.DownloadString(this.config.BaseUrl + '/' + url.TrimStart('/'));
            }
        }
        private void Post(string url)
        {
            if (string.IsNullOrEmpty(this.config.ServerUrl))
                throw new InvalidOperationException("Jenkins ServerUrl has not been set.");

            using (var wc = this.CreateWebClient())
            {
                wc.UploadString(this.config.BaseUrl + '/' + url.TrimStart('/'), string.Empty);
            }
        }
        private void Download(string url, string toFileName)
        {
            if (string.IsNullOrEmpty(this.config.ServerUrl))
                throw new InvalidOperationException("Jenkins ServerUrl has not been set.");

            using (var wc = this.CreateWebClient())
            {
                wc.DownloadFile(this.config.BaseUrl + '/' + url.TrimStart('/'), toFileName);
            }
        }

        public string[] GetJobNames()
        {
            return XDocument.Parse(this.Get("/view/All/api/xml"))
                .Element("allView")
                .Elements("job")
                .Select(x => x.Element("name").Value)
                .ToArray();
        }

        public string GetSpecialBuildNumber(string jobName, string buildNumber)
        {
            return XDocument.Parse(this.Get("job/" + Uri.EscapeUriString(jobName) + "/api/xml"))
                .Descendants(buildNumber)
                .Select(n => n.Element("number").Value)
                .FirstOrDefault();
        }

        public void DownloafArtifact(string jobName, string buildNumber, string fileName)
        {
            this.Download(
                "/job/" + Uri.EscapeUriString(jobName)  + '/' + Uri.EscapeUriString(buildNumber) + "/artifact/*zip*/archive.zip",
                fileName);
        }

        public List<JenkinsBuildArtifact> GetBuildArtifacts(string jobName, string buildNumber)
        {
            return XDocument.Parse(this.Get("job/" + Uri.EscapeUriString(jobName) + "/" + Uri.EscapeUriString(buildNumber) + "/api/xml"))
                .Descendants("artifact")
                .Select(n => new JenkinsBuildArtifact
                {
                    displayPath = n.Element("displayPath").Value,
                    fileName = n.Element("fileName").Value,
                    relativePath = n.Element("relativePath").Value
                })
                .ToList();
        }

        public void DownloafSingleArtifact(string jobName, string buildNumber, string fileName, JenkinsBuildArtifact artifact)
        {
            this.Download(
                "/job/" + Uri.EscapeUriString(jobName)  + '/' + Uri.EscapeUriString(buildNumber) + "/artifact/" + artifact.relativePath,
                fileName);
        }

        public void TriggerBuild(string jobName, string additionalParameters = null)
        {
            var url = "/job/" + Uri.EscapeUriString(jobName) + "/build";
            if (!string.IsNullOrEmpty(additionalParameters))
                url += "WithParameters?" + Uri.EscapeDataString(additionalParameters);
            this.Post(url);
        }

        public string GetNextBuildNumber(string jobName)
        {
            return XDocument.Parse(this.Get("/job/" + Uri.EscapeUriString(jobName)  + "/api/xml?tree=nextBuildNumber"))
                .Descendants("nextBuildNumber")
                .Select(n => n.Value)
                .FirstOrDefault();
        }

        public JenkinsBuild GetBuildInfo(string jobName, string buildNumber)
        {
            try
            {
                var n = XDocument.Parse(this.Get(
                    "/job/" + Uri.EscapeUriString(jobName)  + '/' + Uri.EscapeUriString(buildNumber)
                    + "/api/xml?tree=building,result,number")
                ).Root;
                return new JenkinsBuild
                {
                    building = bool.Parse(n.Element("building").Value),
                    result = n.Elements("result").Select(e => e.Value).FirstOrDefault(),
                    number = n.Elements("number").Select(e => e.Value).FirstOrDefault()
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

    internal class JenkinsBuild
    {
        public bool building;
        public string number;
        public string result;
    }

    internal struct JenkinsBuildArtifact
    {
        public string displayPath;
        public string fileName;
        public string relativePath;
    }
}
