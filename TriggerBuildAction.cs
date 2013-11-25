using System;
using System.Threading;
using System.Xml.Linq;
using Inedo.BuildMaster;
using Inedo.BuildMaster.Extensibility.Actions;
using Inedo.BuildMaster.Web;
using RestSharp;

namespace Inedo.BuildMasterExtensions.Jenkins
{
    /// <summary>
    /// Triggers a build on a Jenkins server.
    /// </summary>
    [ActionProperties(
        "Trigger Jenkins Build",
        "Triggers a build in Jenkins for the specified job.",
        DefaultToLocalServer = true)]
    [CustomEditor(typeof(TriggerBuildActionEditor))]
    [Tag("Jenkins")]
    public sealed class TriggerBuildAction : JenkinsActionBase
    {

        /// <summary>
        /// Gets or sets the additional parameters.
        /// </summary>
        [Persistent]
        public string AdditionalParameters { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether [wait until complete].
        /// </summary>
        [Persistent]
        public bool WaitForCompletion { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TriggerBuildAction"/> class.
        /// </summary>
        public TriggerBuildAction()
        {
        }

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String"/> that represents this instance.
        /// </returns>
        /// <remarks>
        /// This should return a user-friendly string describing what the Action does
        /// and the state of its important persistent properties.
        /// </remarks>
        public override string ToString()
        {
            return string.Format(
                "Triggers a build of the \"{0}\" job in Jenkins{1}.", 
                this.Job,
                Util.ConcatNE(" with the additional parameters \"", this.AdditionalParameters ,"\"")
            );
        }

        protected override void Execute()
        {
            string nextBuildNumber = StartBuild();
            if (string.IsNullOrEmpty(nextBuildNumber))
            {
                this.LogError("The next build number could not be found.");
                return;
            }
            
            this.LogInformation("Build #{0} of {1} was triggered successfully.", nextBuildNumber, this.Job);
            
            if (!this.WaitForCompletion)
                return;
            
            Thread.Sleep(this.GetExtensionConfigurer().Delay * 1000); // give Jenkins some time to create the build

            this.LogInformation("Waiting for build #{0} to finish building in Jenkins...", nextBuildNumber);
            this.WaitForBuildCompletion(nextBuildNumber);
        }

        private string StartBuild()
        {
            var cl = CreateClient();
            cl.Client.FollowRedirects = false;
            RestRequest request; 
            if(string.IsNullOrEmpty(this.AdditionalParameters))
                request = new RestRequest("job/{job}/build", Method.POST);
            else
                request = new RestRequest("job/{job}/buildWithParameters?" + this.AdditionalParameters, Method.POST);
            request.AddUrlSegment("job", this.Job);
            try
            {
                string nextBuildNumber = this.GetNextBuildNumber();
                var resp = cl.Client.Execute(request);
                if ((resp.ResponseStatus != ResponseStatus.Completed) || (resp.StatusCode != System.Net.HttpStatusCode.Found && resp.StatusCode != System.Net.HttpStatusCode.Created && resp.StatusCode != System.Net.HttpStatusCode.OK))
                    throw new Exception(string.Format("Start Build Request error. Response status: {0}, Expected Status Codes: 200, 201 or 302, received: {1}", resp.ResponseStatus.ToString(), (int)resp.StatusCode));

                return nextBuildNumber;
            }
            catch (Exception ex)
            {
                LogError("Unable to trigger a build for job {0}. Error is: {1}", this.Job, ex.ToString());
            }
            return null;
        }

        private void WaitForBuildCompletion(string buildNumber)
        {
            var cl = CreateClient();
            cl.Client.FollowRedirects = false;
            var request = new RestRequest("job/{job}/{build}/api/xml?tree=building,result,number", Method.GET);
            request.AddUrlSegment("job", this.Job);
            request.AddUrlSegment("build", buildNumber);
            try
            {
                JenkinsBuild build;
                do
                {
                    var resp = cl.Client.Execute(request);
                    build = new JenkinsBuild(resp);

                    this.ThrowIfCanceledOrTimeoutExpired();
                    
                    Thread.Sleep(2000);
                }
                while (!build.Completed);

                if (build.Result.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                    this.LogInformation("{0} build #{1} successful. Jenkins reports: {2}", this.Job, buildNumber, build.Result);
                else
                    this.LogError("{0} build #{1} encountered an error. Jenkins reports: {2}", this.Job, buildNumber, build.Result);
            }
            catch (Exception ex)
            {
                this.LogError("Unable to wait for the completion of build {0} for job {1}. Error is: {2}", buildNumber, this.Job, ex.ToString());
            }
        }

        private string GetNextBuildNumber()
        {
            return this.GetJobField("nextBuildNumber");
        }
    }
}
