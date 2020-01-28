using static Inedo.Extensions.Jenkins.InlineIf;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Web;
using System.Security;

namespace Inedo.Extensions.Jenkins.Operations
{
    [DisplayName("Queue Jenkins Build")]
    [Description("Queues a build in Jenkins, optionally waiting for its completion.")]
    [ScriptAlias("Queue-Build")]
    [Tag("builds")]
    [Tag("jenkins")]
    public sealed class QueueJenkinsBuildOperation : JenkinsOperation, IQueueJenkinsBuildArgs
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

        [ScriptAlias("Branch")]
        [DisplayName("Branch name")]
        [SuggestableValue(typeof(BranchNameSuggestionProvider))]
        [Description("The branch name is required for a Jenkins multi-branch project, otherwise should be left empty.")]
        public string BranchName { get; set; }

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

        [Category("Advanced")]
        [ScriptAlias("ProxyRequest")]
        [DisplayName("Use server in context")]
        [Description("When selected, this will proxy the HTTP calls through the server in context instead of using the server Otter or BuildMaster is installed on. If the server in context is SSH-based, then an error will be raised.")]
        public bool ProxyRequest { get; set; }

        [Output]
        [ScriptAlias("JenkinsBuildNumber")]
        [DisplayName("Set build number to variable")]
        [Description("The Jenkins build number can be output into a runtime variable. Requires wait for start or wait for completion.")]
        [PlaceholderText("e.g. $JenkinsBuildNumber")]
        public string JenkinsBuildNumber { get; set; }

        public async override Task ExecuteAsync(IOperationExecutionContext context)
        {
            if (!this.ProxyRequest)
            {
                this.LogDebug($"Making request from {SDK.ProductName} server...");
                await QueueBuildAsync(this, context.CancellationToken);
                return;
            }

            this.LogDebug($"Making request using agent on {context.ServerName}...");
            var jobExec = await context.Agent.GetServiceAsync<IRemoteJobExecuter>();
            using (var job = new QueueBuildRemoteJob(this))
            {
                job.SetProgressOnOperation = this.SetProgress;
                job.MessageLogged += (s, e) => this.OnMessageLogged(e);
                await jobExec.ExecuteJobAsync(job, context.CancellationToken);
            }
        }

        public void SetProgress(OperationProgress progress) => this.progress = progress;

        public override OperationProgress GetProgress() => this.progress;

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            string branchName = config[nameof(this.BranchName)];

            return new ExtendedRichDescription(
                new RichDescription("Queue Jenkins Build"),
                new RichDescription(
                    "for job ", new Hilite(config[nameof(this.JobName)]),
                    IfHasValue(branchName, " on branch ", new Hilite(branchName))
                )
            );
        }

        private static async Task QueueBuildAsync(IQueueJenkinsBuildArgs args, CancellationToken cancellationToken)
        {
            args.LogInformation($"Queueing build for job \"{args.JobName}\"{IfHasValue(args.BranchName, $" on branch \"{args.BranchName}\"")}...");
            
            var client = new JenkinsClient(args.UserName, args.Password, args.ServerUrl, args.CsrfProtectionEnabled, args, cancellationToken);

            var queueItem = await client.TriggerBuildAsync(args.JobName, args.BranchName, args.AdditionalParameters).ConfigureAwait(false);

            args.LogInformation($"Jenkins build queued successfully.");
            args.LogDebug($"Queue item number: {queueItem}");

            if (!args.WaitForStart && !args.WaitForCompletion)
            {
                args.LogDebug("The operation is not configured to wait for the build to start.");
                return;
            }

            string buildNumber;
            string lastReason = null;

            while (true)
            {
                await Task.Delay(2 * 1000, cancellationToken).ConfigureAwait(false);
                var info = await client.GetQueuedBuildInfoAsync(queueItem).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(info.BuildNumber))
                {
                    buildNumber = info.BuildNumber;
                    args.JenkinsBuildNumber = buildNumber;
                    args.LogInformation($"Build number is {buildNumber}.");
                    break;
                }

                if (!string.Equals(lastReason, info.WaitReason))
                {
                    args.LogDebug($"Waiting for build to start... ({info.WaitReason})");
                    lastReason = info.WaitReason;
                }

                args.SetProgress(new OperationProgress(null, info.WaitReason));
            }

            if (args.WaitForCompletion)
            {
                args.LogInformation($"Waiting for build {buildNumber} to complete...");

                JenkinsBuild build;
                int attempts = 5;
                while (true)
                {
                    await Task.Delay(2 * 1000, cancellationToken).ConfigureAwait(false);
                    build = await client.GetBuildInfoAsync(args.JobName, args.BranchName, buildNumber).ConfigureAwait(false);
                    if (build == null)
                    {
                        args.LogDebug("Build information was not returned.");
                        if (attempts > 0)
                        {
                            args.LogDebug($"Reloading build data ({attempts} attempts remaining)...");
                            attempts--;
                            continue;
                        }
                        break;
                    }

                    // reset retry counter
                    attempts = 5;

                    if (!build.Building)
                    {
                        args.LogDebug("Build has finished building.");
                        args.SetProgress(new OperationProgress(100));
                        break;
                    }

                    args.SetProgress(ComputeProgress(build));
                }

                if (string.Equals("success", build?.Result, StringComparison.OrdinalIgnoreCase))
                    args.LogDebug("Build status returned: success");
                else
                    args.LogError("Build not not report success; result was: " + (build?.Result ?? "<not returned>"));
            }
            else
            {
                args.LogDebug("The operation is not configured to wait for build completion.");
            }
        }
        private static OperationProgress ComputeProgress(JenkinsBuild build)
        {
            if (build == null || build.Duration == null || build.EstimatedDuration == null)
            {
                return new OperationProgress((int?)null);
            }

            int progress = ((int)build.Duration * 100) / (int)build.EstimatedDuration;
            return new OperationProgress(Math.Min(progress, 99));
        }

        private sealed class QueueBuildRemoteJob : RemoteJob, IQueueJenkinsBuildArgs
        {
            public string JobName { get; set; }
            public string BranchName { get; set; }
            public string AdditionalParameters { get; set; }
            public bool WaitForStart { get; set; }
            public bool WaitForCompletion { get; set; }
            public bool ProxyRequest { get; set; }
            public string JenkinsBuildNumber { get; set; }
            public string ServerUrl { get; set; }
            public string UserName { get; set; }
            public SecureString Password { get; set; }
            public bool CsrfProtectionEnabled { get; set; }

            public Action<OperationProgress> SetProgressOnOperation { get; set; }

            public QueueBuildRemoteJob()
            {
            }
            public QueueBuildRemoteJob(QueueJenkinsBuildOperation operation)
            {
                this.JobName = operation.JobName;
                this.BranchName = operation.BranchName;
                this.AdditionalParameters = operation.AdditionalParameters;
                this.WaitForStart = operation.WaitForStart;
                this.WaitForCompletion = operation.WaitForCompletion;
                this.ProxyRequest = operation.ProxyRequest;
                this.JenkinsBuildNumber = operation.JenkinsBuildNumber;
                this.ServerUrl = operation.ServerUrl;
                this.UserName = operation.UserName;
                this.Password = operation.Password;
                this.CsrfProtectionEnabled = operation.CsrfProtectionEnabled;
            }

            public override void Serialize(Stream stream)
            {
                using (var writer = new BinaryWriter(stream, InedoLib.UTF8Encoding, true))
                {
                    writer.Write(this.JobName ?? string.Empty);
                    writer.Write(this.AdditionalParameters ?? string.Empty);
                    writer.Write(this.WaitForStart);
                    writer.Write(this.WaitForCompletion);
                    writer.Write(this.ProxyRequest);
                    writer.Write(this.JenkinsBuildNumber ?? string.Empty);
                    writer.Write(this.ServerUrl ?? string.Empty);
                    writer.Write(this.UserName ?? string.Empty);
                    writer.Write(AH.Unprotect(this.Password) ?? string.Empty);
                    writer.Write(this.CsrfProtectionEnabled);
                }
            }
            public override void Deserialize(Stream stream)
            {
                using (var reader = new BinaryReader(stream, InedoLib.UTF8Encoding, true))
                {
                    this.JobName = reader.ReadString();
                    this.AdditionalParameters = reader.ReadString();
                    this.WaitForStart = reader.ReadBoolean();
                    this.WaitForCompletion = reader.ReadBoolean();
                    this.ProxyRequest = reader.ReadBoolean();
                    this.JenkinsBuildNumber = reader.ReadString();
                    this.ServerUrl = reader.ReadString();
                    this.UserName = reader.ReadString();
                    this.Password = AH.CreateSecureString(reader.ReadString());
                    this.CsrfProtectionEnabled = reader.ReadBoolean();
                }
            }

            public override void SerializeResponse(Stream stream, object result)
            {
            }
            public override object DeserializeResponse(Stream stream) => null;

            protected override void DataReceived(byte[] data)
            {
                this.SetProgressOnOperation?.Invoke(new OperationProgress(AH.NullIf((int)data[0], 255), InedoLib.UTF8Encoding.GetString(data, 1, data.Length - 1)));
            }

            public override async Task<object> ExecuteAsync(CancellationToken cancellationToken)
            {
                await QueueBuildAsync(this, cancellationToken).ConfigureAwait(false);
                return null;
            }

            public void SetProgress(OperationProgress p)
            {
                var data = new byte[1 + InedoLib.UTF8Encoding.GetByteCount(p.Message ?? string.Empty)];
                data[0] = (byte)(p.Percent ?? 255);
                InedoLib.UTF8Encoding.GetBytes(p.Message ?? string.Empty, 0, p.Message.Length, data, 1);
                this.Post(data);
            }

            public void Log(IMessage message) => this.Log(message.Level, message.Message);
        }
    }
}
