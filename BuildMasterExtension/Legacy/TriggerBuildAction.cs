using System;
using System.ComponentModel;
using Inedo.BuildMaster.Extensibility.Actions;
using Inedo.BuildMaster.Web;
using Inedo.Documentation;
using Inedo.Serialization;
using Inedo.Extensions.Jenkins;

namespace Inedo.BuildMasterExtensions.Jenkins
{
    [DisplayName("Trigger Jenkins Build")]
    [Description("Triggers a build in Jenkins for the specified job.")]
    [CustomEditor(typeof(TriggerBuildActionEditor))]
    [Tag("jenkins")]
    [ConvertibleToOperation(typeof(TriggerBuildActionConverter))]
    public sealed class TriggerBuildAction : RemoteActionBase
    {
        [Persistent]
        public string JobName { get; set; }

        [Persistent]
        public string AdditionalParameters { get; set; }

        [Persistent]
        public bool WaitForCompletion { get; set; }

        public override ExtendedRichDescription GetActionDescription()
        {
            var ldesc = new RichDescription("in Jenkins");
            if (!string.IsNullOrEmpty(this.AdditionalParameters))
                ldesc.AppendContent(" with additional parameters ", new Hilite(this.AdditionalParameters));
            if (this.WaitForCompletion)
                ldesc.AppendContent(" and ", new Hilite("wait"), " for completion");

            return new ExtendedRichDescription(
                new RichDescription("Trigger ", new Hilite(this.JobName), " Build"),
                ldesc
            );
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
                }

                var status = this.ExecuteRemoteCommand("get-status", nextBuildNumber);

                if (string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
                    this.LogInformation("Build reported success");
                else
                    this.LogError("Build did not report success: " + status);
            }
        }

        protected override string ProcessRemoteCommand(string name, string[] args)
        {
            var client = new JenkinsClient((JenkinsConfigurer)this.GetExtensionConfigurer(), this);

            if (name == "next")
                return client.GetNextBuildNumberAsync(this.JobName).Result();

            else if (name == "build")
            {
                client.TriggerBuildAsync(this.JobName, this.AdditionalParameters).WaitAndUnwrapExceptions();
                return null;
            }
            else if (name == "is-building")
            {
                var build = client.GetBuildInfoAsync(this.JobName, args[0]).Result();
                if (build == null) return bool.TrueString;
                else return build.Building.ToString();
            }

            else if (name == "get-status")
                return client.GetBuildInfoAsync(this.JobName, args[0]).Result().Result;

            else
                throw new ArgumentOutOfRangeException("name");
        }
    }
}
