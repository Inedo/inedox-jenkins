using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml.Linq;
using Inedo.BuildMaster;
using Inedo.BuildMaster.Data;
using Inedo.BuildMaster.Extensibility.Actions;
using Inedo.BuildMaster.Extensibility.Configurers.Extension;
using RestSharp;

namespace Inedo.BuildMasterExtensions.Jenkins
{
    public abstract class JenkinsActionBase : AgentBasedActionBase
    {
        internal JenkinsConfigurer TestConfigurer{get;set;}
        private Dictionary<string, string> artifacts;

        /// <summary>
        /// Gets or sets the build configuration id.
        /// </summary>
        [Persistent]
        public string Job { get; set; }

        /// <summary>
        /// Returns a value indicating whether the extension's configurer currently needs to be
        /// configured.
        /// </summary>
        /// <returns>
        /// True if configurer requires configuration; otherwise false.
        /// </returns>
        /// <remarks>
        /// Unless overridden by an action, this method always returns false.
        /// </remarks>
        public override bool IsConfigurerSettingRequired()
        {
            var configurer = this.GetExtensionConfigurer() as JenkinsConfigurer;

            if (configurer != null)
                return string.IsNullOrEmpty(configurer.ServerUrl);
            else
                return true;
        }

        protected new JenkinsConfigurer GetExtensionConfigurer() 
        {
            var attr = Util.Reflection.GetCustomAttribute<ExtensionConfigurerAttribute>(typeof(JenkinsConfigurer).Assembly);
            if (attr == null)
                return null;

            var row = StoredProcs.ExtensionConfiguration_GetConfiguration(
                attr.ExtensionConfigurerType.FullName + "," + attr.ExtensionConfigurerType.Assembly.GetName().Name,
                null
              ).ExecuteDataRow();

            if (row == null)
                return null;

            return Util.Persistence.DeserializeFromPersistedObjectXml((string)row[TableDefs.ExtensionConfigurations.Extension_Configuration]) as JenkinsConfigurer;
        }

        protected JenkinsClient CreateClient()
        {
            var configurer = (JenkinsConfigurer)GetExtensionConfigurer();
            return new JenkinsClient(configurer);
        }

        public class JenkinsClient
        {
            public RestClient Client { get; set; }
            private JenkinsConfigurer configurer = null;
            private JenkinsClient() { }

            public JenkinsClient(JenkinsConfigurer Configurer)
            {
                configurer = Configurer;
                Client = new RestClient(configurer.BaseUrl);
                if (!string.IsNullOrEmpty(configurer.Username))
                {
                    Client.Authenticator = new HttpBasicAuthenticator(configurer.Username, configurer.Password);
                }
            }
        }

        protected string GetJobField(string field)
        {
            var cl = CreateClient();
            cl.Client.FollowRedirects = false;
            var request = new RestRequest("job/{job}/api/xml/?tree={field}", Method.GET);
            request.AddUrlSegment("job", this.Job);
            request.AddUrlSegment("field", field);
            try
            {
                var resp = cl.Client.Execute(request);
                if ((resp.ResponseStatus != ResponseStatus.Completed) || (resp.StatusCode != System.Net.HttpStatusCode.OK))
                    throw new Exception(string.Format("Get Job Field Request error. Response status: {0}, Expected Status Code: 200, received: {1}", resp.ResponseStatus.ToString(), resp.StatusCode));
                var x = XDocument.Parse(resp.Content);
                if (x.Root.HasElements)
                    return x.Root.Element(field).Value;
            }
            catch (Exception ex)
            {
                LogError("Unable to get the {0} field for job {1}. Error is: {2}", field, this.Job, ex.ToString());
            }
            
            return string.Empty;
        }

        protected JenkinsBuild GetJenkinsBuild(string buildNumber)
        {
            var cl = CreateClient();
            cl.Client.FollowRedirects = false;
            var request = new RestRequest("job/{job}/{buildNumber}/api/xml", Method.GET);
            request.AddUrlSegment("job", this.Job);
            request.AddUrlSegment("buildNumber", buildNumber);
            try
            {
                var resp = cl.Client.Execute(request);
                if ((resp.ResponseStatus != ResponseStatus.Completed) || (resp.StatusCode != System.Net.HttpStatusCode.OK))
                    throw new Exception(string.Format("Get Build by build number request error. Response status: {0}, Expected Status Code: 200, received: {1}", resp.ResponseStatus.ToString(), resp.StatusCode));
                var x = XDocument.Parse(resp.Content);
                return new JenkinsBuild(x);
            }
            catch (Exception ex)
            {
                LogError("Unable to get the latest build for job {0}. Error is: {1}", this.Job, ex.ToString());
            }

            return null;
        }

        protected string GetSpecialBuildNumber(string special)
        {
            var cl = CreateClient();
            cl.Client.FollowRedirects = false;
            var request = new RestRequest("job/{job}/api/xml", Method.GET);
            request.AddUrlSegment("job", this.Job);
            try
            {
                var resp = cl.Client.Execute(request);
                if ((resp.ResponseStatus != ResponseStatus.Completed) || (resp.StatusCode != System.Net.HttpStatusCode.OK))
                    throw new Exception(string.Format("Get Special Build Number Request error. Response status: {0}, Expected Status Code: 200, received: {1}", resp.ResponseStatus.ToString(), resp.StatusCode));
                var x = XDocument.Parse(resp.Content);
                return x.Root.Element(special).Element("number").Value;
            }
            catch (Exception ex)
            {
                LogError("Unable to get the special build number {0} for job {1}. Error is: {2}", special, this.Job, ex.ToString());
            }
            
            return string.Empty;
        }

        protected IDictionary<string, string> ListArtifacts(string buildNumber)
        {
            if (null != artifacts)
                return artifacts; 

            var cl = CreateClient();
            cl.Client.FollowRedirects = false;
            var request = new RestRequest("job/{job}/{build}/api/xml", Method.GET);
            request.AddUrlSegment("job", this.Job);
            request.AddUrlSegment("build", buildNumber);
            try
            {
                var resp = cl.Client.Execute(request);
                if ((resp.ResponseStatus != ResponseStatus.Completed) || (resp.StatusCode != System.Net.HttpStatusCode.OK))
                    throw new Exception(string.Format("List Artifacts Request error. Response status: {0}, Expected Status Code: 200, received: {1}", resp.ResponseStatus.ToString(), resp.StatusCode));
                artifacts = (from a in XElement.Parse(resp.Content).Descendants("artifact") select new { FileName = a.Element("fileName").Value, 
                    RelPath = a.Element("relativePath").Value }).ToDictionary(a => a.FileName, a => a.RelPath);
                return artifacts;
            }
            catch (Exception ex)
            {
                LogError("Unable to list the artifacts for build number {0} for job {1}. Error is: {2}", buildNumber, this.Job, ex.ToString());
            }

            return new Dictionary<string, string>();
        }

        protected int GetBuildNumber(string value)
        {
            int retVal = 0;
            // is the value a numeric build number?
            if(int.TryParse(value, out retVal)) 
                return retVal;
            // if not then see if it is a "special" token
            int.TryParse(GetSpecialBuildNumber(value), out retVal);
            return retVal;
        }

        protected bool GetArtifact(int buildNumber, string relativePath, string filePath)
        {
            var cl = CreateClient();
            cl.Client.FollowRedirects = false;
            var request = new RestRequest("job/{job}/{build}/artifact/{relpath}", Method.GET);
            request.AddUrlSegment("job", this.Job);
            request.AddUrlSegment("build", buildNumber.ToString());
            request.AddUrlSegment("relpath", relativePath);            
            try
            {
                var resp = cl.Client.Execute(request);
                if ((resp.ResponseStatus != ResponseStatus.Completed) || (resp.StatusCode != System.Net.HttpStatusCode.OK))
                    throw new Exception(string.Format("Get Artifact Request error. Response status: {0}, Expected Status Code: 200, received: {1}", resp.ResponseStatus.ToString(), resp.StatusCode));
                File.WriteAllBytes(filePath, resp.RawBytes);
                return true;
            }
            catch (Exception ex)
            {
                LogError("Unable to get the artifact: {0} for job {1} build {2}. Error is: {3}", relativePath, this.Job, buildNumber, ex.ToString());
            }

            return false;
        }

        protected sealed class JenkinsBuild
        {
            public bool Found { get; private set; }
            public bool Building { get; private set; }
            public string Number { get; private set; }
            public string Result { get; private set; }
            public bool Completed { get { return this.Found && !this.Building; } }

            public JenkinsBuild(XDocument x)
            {
                InitializeFromXml(x);
            }

            public JenkinsBuild(IRestResponse resp)
            {
                this.Found = resp.ResponseStatus == ResponseStatus.Completed 
                    && resp.StatusCode == HttpStatusCode.OK;

                InitializeFromXml(XDocument.Parse(resp.Content));
            }

            private void InitializeFromXml(XDocument x) 
            {
                if (!x.Root.HasElements)
                    return;

                if (x.Root.Element("building") != null)
                    this.Building = bool.Parse(x.Root.Element("building").Value);
                if (x.Root.Element("number") != null)
                    this.Number = x.Root.Element("number").Value;
                if (x.Root.Element("result") != null)
                    this.Result = x.Root.Element("result").Value;
            }
        }

        protected abstract override void Execute();

        public abstract override string ToString();
    }
}
