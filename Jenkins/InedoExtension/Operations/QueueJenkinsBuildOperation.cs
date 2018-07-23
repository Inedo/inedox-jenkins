using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Web;

namespace Inedo.Extensions.Jenkins.Operations
{
    [DisplayName("Queue Jenkins Build")]
    [Description("Queues a build in Jenkins, optionally waiting for its completion.")]
    [ScriptAlias("Queue-Build")]
    [Tag("builds")]
    [Tag("jenkins")]
    public sealed class QueueJenkinsBuildOperation : JenkinsOperation
    {
        private volatile OperationProgress progress;

        [ScriptAlias("Credentials")]
        [DisplayName("Credentials")]
        public override string CredentialName { get; set; }

        [Required]
        [ScriptAlias("Job")]
        [DisplayName("Job name")]
        [SuggestableValue(typeof(JobNameSuggestionProvider))]
        public string JobName { get; set; }

        [Category("Advanced")]
        [ScriptAlias("AdditionalParameters")]
        [DisplayName("Additional parameters")]
        [Description("Additional case-sensitive parameters for the build in the format: token=TOKEN&PARAMETER=Value")]
        public string AdditionalParameters { get; set; }

        [Category("Advanced")]
        [ScriptAlias("WaitForStart")]
        [DisplayName("Wait for start")]
        [Description("Ignored if wait for completion is true.")]
        [DefaultValue(true)]
        [PlaceholderText("true")]
        public bool WaitForStart { get; set; } = true;

        [Category("Advanced")]
        [ScriptAlias("WaitForCompletion")]
        [DisplayName("Wait for completion")]
        [DefaultValue(true)]
        [PlaceholderText("true")]
        public bool WaitForCompletion { get; set; } = true;

        [Output]
        [ScriptAlias("JenkinsBuildNumber")]
        [DisplayName("Set build number to variable")]
        [Description("The Jenkins build number can be output into a runtime variable. Requires wait for start or wait for completion.")]
        [PlaceholderText("e.g. $JenkinsBuildNumber")]
        public string JenkinsBuildNumber { get; set; }

        public async override Task ExecuteAsync(IOperationExecutionContext context)
        {
            this.LogInformation("Queueing build in Jenkins...");

            var client = new JenkinsClient(this, this);

            var queueItem = await client.TriggerBuildAsync(this.JobName, this.AdditionalParameters).ConfigureAwait(false);

            this.LogInformation($"Jenkins build queued successfully.");
            this.LogDebug($"Queue item number: {queueItem}");

            if (!this.WaitForStart && !this.WaitForCompletion)
            {
                this.LogDebug("The operation is not configured to wait for the build to start.");
            }

            string buildNumber;
            string lastReason = null;

            while (true)
            {
                await Task.Delay(2 * 1000, context.CancellationToken).ConfigureAwait(false);
                var info = await client.GetQueuedBuildInfoAsync(queueItem).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(info.BuildNumber))
                {
                    buildNumber = info.BuildNumber;
                    this.JenkinsBuildNumber = buildNumber;
                    this.LogInformation($"Build number is {buildNumber}.");
                    break;
                }

                if (!string.Equals(lastReason, info.WaitReason))
                {
                    this.LogDebug($"Waiting for build to start... ({info.WaitReason})");
                    lastReason = info.WaitReason;
                }

                this.progress = new OperationProgress(null, info.WaitReason);
            }

            if (this.WaitForCompletion)
            {
                this.LogInformation($"Waiting for build {buildNumber} to complete...");

                JenkinsBuild build;
                int attempts = 5;
                while (true)
                {
                    await Task.Delay(2 * 1000, context.CancellationToken).ConfigureAwait(false);
                    build = await client.GetBuildInfoAsync(this.JobName, buildNumber).ConfigureAwait(false);
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
                        this.progress = new OperationProgress(100);
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
            return this.progress;
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
                this.progress = new OperationProgress((int?)null);
            }
            else
            {
                int progress = ((int)build.Duration * 100) / (int)build.EstimatedDuration;
                this.progress = new OperationProgress(Math.Min(progress, 99));
            }
        }
    }
}
