using System;
using System.ComponentModel;
using System.Threading.Tasks;
#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.BuildMaster.Web.Controls;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Operations;
using Inedo.Otter.Web.Controls;
#endif
using Inedo.Diagnostics;
using Inedo.Documentation;

namespace Inedo.Extensions.Jenkins.Operations
{
    [DisplayName("Queue Jenkins Build")]
    [Description("Queues a build in Jenkins, optionally waiting for its completion.")]
    [ScriptAlias("Queue-Build")]
    [Tag("builds")]
    [Tag("jenkins")]
    public sealed class QueueJenkinsBuildOperation : JenkinsOperation
    {
        private int progressPercent;

        [ScriptAlias("Credentials")]
        [DisplayName("Credentials")]
        public override string CredentialName { get; set; }

        [Required]
        [ScriptAlias("Job")]
        [DisplayName("Job name")]
        [SuggestibleValue(typeof(JobNameSuggestionProvider))]
        public string JobName { get; set; }

        [Category("Advanced")]
        [ScriptAlias("AdditionalParameters")]
        [DisplayName("Additional parameters")]
        [Description("Additional case-sensitive parameters for the build in the format: token=TOKEN&PARAMETER=Value")]
        public string AdditionalParameters { get; set; }

        [Category("Advanced")]
        [ScriptAlias("WaitForCompletion")]
        [DisplayName("Wait for completion")]
        [DefaultValue(true)]
        [PlaceholderText("true")]
        public bool WaitForCompletion { get; set; } = true;

        [Output]
        [ScriptAlias("JenkinsBuildNumber")]
        [DisplayName("Set build number to variable")]
        [Description("The Jenkins build number can be output into a runtime variable")]
        [PlaceholderText("e.g. $JenkinsBuildNumber")]
        public string JenkinsBuildNumber { get; set; }

        public async override Task ExecuteAsync(IOperationExecutionContext context)
        {
            this.LogInformation("Queueing build in Jenkins...");

            this.LogDebug("Determining next build number...");
            var client = new JenkinsClient((IJenkinsConnectionInfo)this, (ILogger)this);
            string nextBuildNumber = await client.GetNextBuildNumberAsync(this.JobName).ConfigureAwait(false);

            this.LogInformation($"Triggering Jenkins build #{nextBuildNumber}...");

            await client.TriggerBuildAsync(this.JobName, this.AdditionalParameters).ConfigureAwait(false);

            this.LogInformation("Jenkins build queued successfully.");

            this.JenkinsBuildNumber = nextBuildNumber;

            if (this.WaitForCompletion)
            {
                this.LogInformation($"Waiting for build {nextBuildNumber} to complete...");

                JenkinsBuild build;
                int attempts = 5;
                while (true)
                {
                    await Task.Delay(2 * 1000, context.CancellationToken).ConfigureAwait(false);
                    build = await client.GetBuildInfoAsync(this.JobName, nextBuildNumber).ConfigureAwait(false);
                    if (build == null)
                    {
                        this.LogDebug("Build information was not returned.");
                        if (attempts > 0)
                        {
                            this.LogDebug($"Reloading build data ({attempts} attempts remaining)...");
                            attempts--;
                            continue;
                        }
                        break;
                    }

                    this.SetProgress(build);

                    if (!build.Building)
                    {
                        this.LogDebug("Build has finished building.");
                        this.progressPercent = 100;
                        break;
                    }
                }

                if (string.Equals("success", build?.Result, StringComparison.OrdinalIgnoreCase))
                    this.LogDebug("Build status returned: success");
                else
                    this.LogError("Build not not report success; result was: " + (build?.Result ?? "<not returned>"));
            }
            else
            {
                this.LogDebug("The operation is not configured to wait for build completion.");
            }
        }

        public override OperationProgress GetProgress()
        {
            return new OperationProgress(this.progressPercent);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription("Queue Jenkins Build"),
                new RichDescription(
                    "for job ",
                    new Hilite(config[nameof(this.JobName)])
                )
            );
        }

        private void SetProgress(JenkinsBuild build)
        {
            if (build == null || build.Duration == null || build.EstimatedDuration == null)
            {
                this.progressPercent = 0;
            }
            else
            {
                int progress = ((int)build.Duration * 100) / (int)build.EstimatedDuration;
                this.progressPercent = Math.Min(progress, 99);
            }
        }
    }
}
