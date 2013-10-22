using System;
using System.Threading;
using System.Xml;
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
    public class TriggerBuildAction : JenkinsActionBase
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
            if (!StartBuild())
                return;
            LogInformation("Build of {0} was triggered successfully.", this.Job);
            if (!this.WaitForCompletion)
                return;
            Thread.Sleep(((JenkinsConfigurer)this.GetExtensionConfigurer()).Delay * 1000); // give Jenkins some time to create the build
            var latestBuild = LatestBuild();
            if (!latestBuild.Building)
            {
                LogError("BuildMaster has triggered a build in Jenkins for the {0} job, but Jenkins indicates that there are no builds running at this time for that job, therefore BuildMaster cannot wait until the build completes.", this.Job);
                return;
            }
            WaitForBuildCompletion(latestBuild);
        }

        internal protected bool StartBuild()
        {
            bool retVal = true;
            var cl = CreateClient();
            cl.Client.FollowRedirects = false;
            RestRequest request; 
            if(string.IsNullOrEmpty(this.AdditionalParameters))
                request = new RestRequest("job/{job}/build", Method.POST);
            else
                request = new RestRequest("job/{job}/build?" + this.AdditionalParameters, Method.POST);
            request.AddUrlSegment("job", this.Job);
            try
            {
                var resp = cl.Client.Execute(request);
                if ((resp.ResponseStatus != ResponseStatus.Completed) || (resp.StatusCode != System.Net.HttpStatusCode.Found))
                    throw new Exception(string.Format("Start Build Request error. Response status: {0}, Expected Status Code: 302, received: {1}", resp.ResponseStatus.ToString(), resp.StatusCode));
                var content = resp.Content;
            }
            catch (Exception ex)
            {
                retVal = false;
                LogError("Unable to trigger a build for job {0}. Error is: {1}", this.Job, ex.ToString());
            }
            return retVal;
        }

    }
}
