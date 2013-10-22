using System;
using System.Net;
using System.Text;
using System.IO;

using Inedo.BuildMaster;
using Inedo.BuildMaster.Extensibility.Actions;
using System.Linq;
using System.Xml.Linq;
using System.Collections;
using System.Collections.Generic;

using RestSharp;
using RestSharp.Extensions;
using Inedo.BuildMaster.Extensibility.Configurers.Extension;
using Inedo.BuildMaster.Data;

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

        internal protected string GetJobField(string Field)
        {
            string retVal = "";
            var cl = CreateClient();
            cl.Client.FollowRedirects = false;
            var request = new RestRequest("job/{job}/api/xml/?tree={field}", Method.GET);
            request.AddUrlSegment("job", this.Job);
            request.AddUrlSegment("field", Field);
            try
            {
                var resp = cl.Client.Execute(request);
                if ((resp.ResponseStatus != ResponseStatus.Completed) || (resp.StatusCode != System.Net.HttpStatusCode.OK))
                    throw new Exception(string.Format("Get Job Field Request error. Response status: {0}, Expected Status Code: 200, received: {1}", resp.ResponseStatus.ToString(), resp.StatusCode));
                var x = XDocument.Parse(resp.Content);
                if (x.Root.HasElements)
                    retVal = x.Root.Element(Field).Value;
            }
            catch (Exception ex)
            {
                LogError("Unable to get the {0} field for job {1}. Error is: {2}", Field, this.Job, ex.ToString());
            }
            return retVal;
        }

        internal protected JenkinsBuild LatestBuild()
        {
            JenkinsBuild retVal = null;
            var cl = CreateClient();
            cl.Client.FollowRedirects = false;
            var request = new RestRequest("job/{job}/lastBuild/api/xml", Method.GET);
            request.AddUrlSegment("job", this.Job);
            try
            {
                var resp = cl.Client.Execute(request);
                if ((resp.ResponseStatus != ResponseStatus.Completed) || (resp.StatusCode != System.Net.HttpStatusCode.OK))
                    throw new Exception(string.Format("Get Latest Job Request error. Response status: {0}, Expected Status Code: 200, received: {1}", resp.ResponseStatus.ToString(), resp.StatusCode));
                var x = XDocument.Parse(resp.Content);
                retVal = new JenkinsBuild(x);
            }
            catch (Exception ex)
            {
                LogError("Unable to get the latest build for job {0}. Error is: {1}", this.Job, ex.ToString());
            }
            return retVal;
        }

        internal void WaitForBuildCompletion(JenkinsBuild Build)
        {
            var cl = CreateClient();
            var startTime = DateTime.Now;
            cl.Client.FollowRedirects = false;
            var request = new RestRequest("job/{job}/{build}/api/xml?tree=building,result", Method.GET);
            request.AddUrlSegment("job", this.Job);
            request.AddUrlSegment("build", Build.Number );
            try
            {
                bool done = false;
                XDocument x = null;
                while(!done)
                {
                    var resp = cl.Client.Execute(request);
                    if ((resp.ResponseStatus != ResponseStatus.Completed) || (resp.StatusCode != System.Net.HttpStatusCode.OK))
                        throw new Exception(string.Format("Wait For Completion Request error. Response status: {0}, Expected Status Code: 200, received: {1}", resp.ResponseStatus.ToString(), resp.StatusCode));
                    x = XDocument.Parse(resp.Content);
                    if (x.Root.HasElements && null != x.Root.Element("building"))
                        done = !bool.Parse(x.Root.Element("building").Value);
                    else
                        throw new Exception(string.Format("Wait For Completion Request error. Element \"building\" does not exist in Jenkins API response. Content: {0}", resp.Content));
                    if(DateTime.Now.Subtract(startTime) > TimeSpan.FromHours(24))
                        throw new Exception(string.Format("Wait For Completion timeout error. The build has taken more than 24 hours.", resp.Content));
                    System.Threading.Thread.Sleep(2000);
                }
                string result = "";
                if ((null != x) && x.Root.HasElements && (null != x.Root.Element("result")))
                    result = x.Root.Element("result").Value;
                if(result.ToUpperInvariant() == "SUCCESS".ToUpperInvariant())
                    LogInformation("{0} build #{1} successful. Jenkins reports: {2}", this.Job, Build.Number, result);
                else
                    LogError("{0} build #{1} encountered an error. Jenkins reports: {2}", this.Job, Build.Number, result );
            }
            catch (Exception ex)
            {
                LogError("Unable to wait for the completion of build {0} for job {1}. Error is: {2}", Build.Number, this.Job, ex.ToString());
            }
        }

        internal protected string GetSpecialBuildNumber(string Special)
        {
            string retVal = "";
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
                return x.Root.Element(Special).Element("number").Value;
            }
            catch (Exception ex)
            {
                LogError("Unable to get the special build number {0} for job {1}. Error is: {2}", Special,this.Job, ex.ToString());
            }
            return retVal;
        }

        internal protected System.Collections.Generic.IDictionary<string, string> ListArtifacts(string BuildNumber)
        {
            if (null != artifacts)
                return artifacts; 
            var retVal = new System.Collections.Generic.Dictionary<string, string>();
            var cl = CreateClient();
            cl.Client.FollowRedirects = false;
            var request = new RestRequest("job/{job}/{build}/api/xml", Method.GET);
            request.AddUrlSegment("job", this.Job);
            request.AddUrlSegment("build", BuildNumber);
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
                LogError("Unable to list the artifacts for build number {0} for job {1}. Error is: {2}", BuildNumber, this.Job, ex.ToString());
            }
            return retVal;
        }

        internal protected int GetBuildNumber(string Value)
        {
            int retVal = 0;
            // is the value a numeric build nuber?
            if(int.TryParse(Value,out retVal)) 
                return retVal;
            // if not then see if it is a "special" token
            int.TryParse(GetSpecialBuildNumber(Value), out retVal);
            return retVal;
        }

        internal protected bool GetArtifact(int BuildNumber, string RelativePath, string FilePath)
        {
            bool retVal = false;
            var cl = CreateClient();
            cl.Client.FollowRedirects = false;
            var request = new RestRequest("job/{job}/{build}/artifact/{relpath}", Method.GET);
            request.AddUrlSegment("job", this.Job);
            request.AddUrlSegment("build", BuildNumber.ToString());
            request.AddUrlSegment("relpath", RelativePath);            
            try
            {
                var resp = cl.Client.Execute(request);
                if ((resp.ResponseStatus != ResponseStatus.Completed) || (resp.StatusCode != System.Net.HttpStatusCode.OK))
                    throw new Exception(string.Format("Get Artifact Request error. Response status: {0}, Expected Status Code: 200, received: {1}", resp.ResponseStatus.ToString(), resp.StatusCode));
                File.WriteAllBytes(FilePath, resp.RawBytes);
                return true;
            }
            catch (Exception ex)
            {
                LogError("Unable to get the artifact: {0} for job {1} build {2}. Error is: {3}", RelativePath, this.Job, BuildNumber,ex.ToString());
            }

            return retVal;
        }

        internal protected class JenkinsBuild
        {
            public bool Building { get; set; }
            public string Number { get; set; }
            private JenkinsBuild() { }
            public JenkinsBuild(XDocument x)
            {
                if(x.Root.HasElements && (null != x.Root.Element("building")))
                    this.Building = bool.Parse(x.Root.Element("building").Value);
                if (x.Root.HasElements && (null != x.Root.Element("number")))
                    this.Number = x.Root.Element("number").Value;
            }
        }

        protected override void Execute()
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            throw new NotImplementedException();
        }
    }
}
