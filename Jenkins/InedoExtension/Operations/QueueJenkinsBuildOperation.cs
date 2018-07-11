using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Agents;
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
        private int? progressPercent;

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
        [ScriptAlias("WaitForCompletion")]
        [DisplayName("Wait for completion")]
        [DefaultValue(true)]
        [PlaceholderText("true")]
        public bool WaitForCompletion { get; set; } = true;

        [Category("Advanced")]
        [ScriptAlias("ProxyRequest")]
        [DisplayName("Use server in context")]
        [Description("When selected, this will proxy the HTTP calls through the server is in context instead of using the server Otter or BuildMaster is installed on. If the server in context is SSH-based, then an error will be raised.")]
        [DefaultValue(true)]
        public bool ProxyRequest { get; set; } = true;

        [Output]
        [ScriptAlias("JenkinsBuildNumber")]
        [DisplayName("Set build number to variable")]
        [Description("The Jenkins build number can be output into a runtime variable")]
        [PlaceholderText("e.g. $JenkinsBuildNumber")]
        public string JenkinsBuildNumber { get; set; }

        public async override Task ExecuteAsync(IOperationExecutionContext context)
        {
            if (!this.ProxyRequest)
            {
                await this.QueueBuildAsync(progress => this.progressPercent = progress, context.CancellationToken).ConfigureAwait(false);
                return;
            }

            var jobExec = await context.Agent.GetServiceAsync<IRemoteJobExecuter>().ConfigureAwait(false);
            using (var job = new QueueBuildRemoteJob(this))
            {
                job.MessageLogged += (s, e) => this.OnMessageLogged(e);
                await jobExec.ExecuteJobAsync(job, context.CancellationToken).ConfigureAwait(false);
            }
        }

        private async Task QueueBuildAsync(Action<int?> setProgress, CancellationToken cancellationToken)
        {
            this.LogInformation("Queueing build in Jenkins...");

            this.LogDebug("Determining next build number...");
            var client = new JenkinsClient(this, this);
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
                    await Task.Delay(2 * 1000, cancellationToken).ConfigureAwait(false);
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

                    // reset retry counter
                    attempts = 5;

                    if (!build.Building)
                    {
                        this.LogDebug("Build has finished building.");
                        setProgress(100);
                        break;
                    }

                    setProgress(ComputeProgress(build));
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

        private static int? ComputeProgress(JenkinsBuild build)
        {
            if (build == null || build.Duration == null || build.EstimatedDuration == null)
            {
                return null;
            }

            int progress = ((int)build.Duration * 100) / (int)build.EstimatedDuration;
            return Math.Min(progress, 99);
        }

        private sealed class QueueBuildRemoteJob : RemoteJob
        {
            private QueueJenkinsBuildOperation Operation { get; set; }

            public QueueBuildRemoteJob(QueueJenkinsBuildOperation operation)
            {
                this.Operation = operation;
            }

            public override void Serialize(Stream stream)
            {
                new BinaryFormatter().Serialize(stream, this.Operation);
            }

            public override void Deserialize(Stream stream)
            {
                this.Operation = (QueueJenkinsBuildOperation)new BinaryFormatter().Deserialize(stream);
                this.Operation.MessageLogged += (s, e) => this.Log(e.Level, e.Message);
            }

            public override void SerializeResponse(Stream stream, object result)
            {
                return;
            }

            public override object DeserializeResponse(Stream stream)
            {
                return null;
            }

            protected override void DataReceived(byte[] data)
            {
                if (data.Length == 1)
                {
                    this.Operation.progressPercent = AH.NullIf((int)data[0], 255);
                }
            }

            public override async Task<object> ExecuteAsync(CancellationToken cancellationToken)
            {
                await this.Operation.QueueBuildAsync(p => this.Post(new byte[1] { (byte)(p ?? 255) }), cancellationToken).ConfigureAwait(false);

                return null;
            }
        }
    }
}
