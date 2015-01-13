using System;
using Inedo.BuildMaster;
using Inedo.BuildMaster.Extensibility.Actions;
using Inedo.BuildMaster.Web;

namespace Inedo.BuildMasterExtensions.Jenkins
{
    [ActionProperties(
        "Trigger Jenkins Build",
        "Triggers a build in Jenkins for the specified job.",
        DefaultToLocalServer = true)]
    [CustomEditor(typeof(TriggerBuildActionEditor))]
    [Tag("jenkins")]
    public sealed class TriggerBuildAction : RemoteActionBase
    {
        [Persistent]
        public string JobName { get; set; }

        [Persistent]
        public string AdditionalParameters { get; set; }

        [Persistent]
        public bool WaitForCompletion { get; set; }

        public override ActionDescription GetActionDescription()
        {
            var ldesc = new LongActionDescription("in Jenkins");
            if (!string.IsNullOrEmpty(this.AdditionalParameters))
                ldesc.AppendContent(" with additional parameters ", new Hilite(this.AdditionalParameters));
            if (this.WaitForCompletion)
                ldesc.AppendContent(" and ", new Hilite("wait"), " for completion");

            return new ActionDescription(
                new ShortActionDescription("Trigger ", new Hilite(this.JobName), " Build"),
                ldesc);
        }

        protected override void Execute()
        {
            this.LogDebug("Determining next Jenkins build number...");
            var nextBuildNumber = this.ExecuteRemoteCommand("next");

            this.LogInformation("Triggering Jenkins Build #{0}...", nextBuildNumber);
            this.ExecuteRemoteCommand("build");

            if (this.WaitForCompletion)
            {
                this.LogInformation("Waiting for build to complete...", nextBuildNumber);
                while (true)
                {
                    this.Context.CancellationToken.WaitHandle.WaitOne(2000);
                    if (this.Context.CancellationToken.IsCancellationRequested) return;

                    if (!bool.Parse(this.ExecuteRemoteCommand("is-building", nextBuildNumber))) break;
                    //if (!bool.Parse(this.ProcessRemoteCommand("is-building", new [] {nextBuildNumber }))) break;
                }

                var status = this.ExecuteRemoteCommand("get-status", nextBuildNumber);
                    //this.ProcessRemoteCommand("get-status", new [] { nextBuildNumber});

                if (string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
                    this.LogInformation("Build reported success");
                else
                    this.LogError("Build did not report success: " + status);
            }
        }

        protected override string ProcessRemoteCommand(string name, string[] args)
        {
            var client = new JenkinsClient((JenkinsConfigurer)this.GetExtensionConfigurer());
            
            if (name == "next")
                return client.GetNextBuildNumber(this.JobName);

            else if (name == "build")
            {
                client.TriggerBuild(this.JobName, this.AdditionalParameters);
                return null;
            }
            else if (name == "is-building")
            {
                var build = client.GetBuildInfo(this.JobName, args[0]);
                if (build == null) return bool.TrueString;
                else return build.Building.ToString();
            }

            else if (name == "get-status")
                return client.GetBuildInfo(this.JobName, args[0]).Result;

            else
                throw new ArgumentOutOfRangeException("name");

        }
    }
}
