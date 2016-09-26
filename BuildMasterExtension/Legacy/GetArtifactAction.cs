using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Inedo.Agents;
using Inedo.BuildMaster;
using Inedo.BuildMaster.Extensibility.Actions;
using Inedo.BuildMaster.Web;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.IO;
using Inedo.Serialization;
using Inedo.Extensions.Jenkins;

namespace Inedo.BuildMasterExtensions.Jenkins
{
    [DisplayName("Download Jenkins Artifact")]
    [Description("Downloads artifact files from a Jenkins server.")]
    [RequiresInterface(typeof(IFileOperationsExecuter))]
    [CustomEditor(typeof(GetArtifactActionEditor))]
    [Tag("jenkins")]
    [ConvertibleToOperation(typeof(GetArtifactActionConverter))]
    public sealed class GetArtifactAction : AgentBasedActionBase, IMissingPersistentPropertyHandler
    {
        [Persistent]
        public string JobName { get; set; }

        [Persistent]
        public string ArtifactName { get; set; }

        [Persistent]
        public string BuildNumber { get; set; }

        [Persistent]
        public bool ExtractFilesToTargetDirectory { get; set; } = true;

        public override ExtendedRichDescription GetActionDescription()
        {
            return new ExtendedRichDescription(
                new RichDescription("Download ", new Hilite(this.JobName), " Artifact"),
                new RichDescription(
                    this.ExtractFilesToTargetDirectory ? "" : "as zip file ",
                    "from Jenkins to ", new DirectoryHilite(this.OverriddenTargetDirectory)
                )
            );
        }

        private JenkinsClient GetClient()
        {
            var configurer = (JenkinsConfigurer)this.GetExtensionConfigurer();
            return new JenkinsClient(configurer, this);
        }
        
        private void DownloadZip()
        {
            var config = (JenkinsConfigurer)this.GetExtensionConfigurer();

            var remoteFileName = this.ExtractFilesToTargetDirectory
                ? PathEx.Combine(this.Context.TempDirectory, "archive.zip")
                : PathEx.Combine(this.Context.TargetDirectory, "archive.zip");

            var remote = this.Context.Agent.TryGetService<IRemoteMethodExecuter>();

            var fileOps = this.Context.Agent.GetService<IFileOperationsExecuter>();

            if (remote != null)
            {
                this.LogDebug("Downloading to {0}...", remoteFileName);
                remote.InvokeAction<JenkinsConfigurer, string, string, string>(
                    (cfg, job, bld, fil) => new JenkinsClient(cfg, this).DownloadArtifactAsync(job, bld, fil).WaitAndUnwrapExceptions(),
                    (JenkinsConfigurer)this.GetExtensionConfigurer(), this.JobName, this.BuildNumber, remoteFileName);
            }
            else
            {
                var localFileName = Path.GetTempFileName();

                this.LogDebug("Downloading to {0}...", localFileName);
                new JenkinsClient(config, this).DownloadArtifactAsync(this.JobName, this.BuildNumber, localFileName).WaitAndUnwrapExceptions();

                this.LogDebug("Transferring to server...", remoteFileName);
                using (var localFile = File.OpenRead(localFileName))
                using (var remoteFile = fileOps.OpenFile(remoteFileName, FileMode.Create, FileAccess.Write))
                {
                    localFile.CopyTo(remoteFile);
                }
            }

            if (this.ExtractFilesToTargetDirectory)
            {
                this.LogDebug("Extracting to...", this.Context.TargetDirectory);
                fileOps.ExtractZipFile(remoteFileName, this.Context.TargetDirectory, true);
            }

            this.LogInformation("Artifact successfully downloaded.");
        }

        private void DownloadFile(JenkinsBuildArtifact artifact)
        {
            var config = (JenkinsConfigurer)this.GetExtensionConfigurer();

            var remoteFileName = PathEx.Combine(this.Context.TargetDirectory, artifact.FileName);
            var fileOps = this.Context.Agent.GetService<IFileOperationsExecuter>();
            var remote = this.Context.Agent.TryGetService<IRemoteMethodExecuter>();

            if (remote != null)
            {
                this.LogDebug("Downloading to {0}...", remoteFileName);
                remote.InvokeMethod(
                    new Action<JenkinsConfigurer, string, string, string, JenkinsBuildArtifact, ILogger>(DownloadSingleArtifactInternal),
                    (JenkinsConfigurer)this.GetExtensionConfigurer(), 
                    this.JobName, 
                    this.BuildNumber, 
                    remoteFileName,
                    artifact,
                    this
                );
            }
            else
            {
                var localFileName = Path.GetTempFileName();

                this.LogDebug("Downloading to {0}...", localFileName);
                new JenkinsClient(config, this).DownloadSingleArtifactAsync(this.JobName, this.BuildNumber, localFileName, artifact).WaitAndUnwrapExceptions();

                this.LogDebug("Transferring to server...", remoteFileName);
                using (var localFile = File.OpenRead(localFileName))
                using (var remoteFile = fileOps.OpenFile(remoteFileName, FileMode.Create, FileAccess.Write))
                {
                    localFile.CopyTo(remoteFile);
                }
            }
        }

        private static void DownloadSingleArtifactInternal(JenkinsConfigurer configurer, string job, string buildNumber, string fileName, JenkinsBuildArtifact artifact, ILogger logger)
        {
            var client = new JenkinsClient(configurer, logger);
            client.DownloadSingleArtifactAsync(job, buildNumber, fileName, artifact).WaitAndUnwrapExceptions();
        }
        
        protected override void Execute()
        {
            if (AH.ParseInt(this.BuildNumber) == null)
            {
                var client = GetClient();
                
                this.LogDebug("Looking up {0}...", this.BuildNumber);
                this.BuildNumber = client.GetSpecialBuildNumberAsync(this.JobName, this.BuildNumber).Result();
                this.LogInformation("Using Jenkins build number {0}.", this.BuildNumber);
            }

            if (string.IsNullOrEmpty(this.ArtifactName) || this.ArtifactName == "*")
            {
                this.LogDebug("Artifact filter not specified; downloading all artifacts as zip...");
                this.DownloadZip();
            }
            else
            {
                var client = GetClient();
                
                var artifacts = client.GetBuildArtifactsAsync(this.JobName, this.BuildNumber).Result();
                this.LogDebug("Build contains {0} build artifacts.", artifacts.Count);
                if (artifacts.Count == 0)
                {
                    this.LogWarning("Build contains no artifacts");
                    return;
                }

                foreach (var artifact in artifacts)
                    this.LogDebug("Found artifact: (fileName=\"{0}\", relativePath=\"{1}\", displayPath=\"{2}\")", artifact.FileName, artifact.RelativePath, artifact.DisplayPath);

                var pattern = "^" + Regex.Escape(this.ArtifactName)
                    .Replace(@"\*",".*")
                    .Replace(@"\?",".") + "$";

                var filteredArtifacts = artifacts
                    .Where(a => Regex.IsMatch(a.FileName, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                    .ToList();

                this.LogDebug("{0} artifacts match pattern \"{1}\".", filteredArtifacts.Count, pattern);
                if (filteredArtifacts.Count == 0)
                {
                    this.LogWarning("Build contains no filtered artifacts");
                    return;
                }

                if (!this.ExtractFilesToTargetDirectory)
                    this.LogWarning("ExtractFilesToTargetDirectory will be ignored, as individual file(s) are being downloaded.");

                foreach (var artifact in filteredArtifacts)
                    this.DownloadFile(artifact);
            }
        }

        void IMissingPersistentPropertyHandler.OnDeserializedMissingProperties(IReadOnlyDictionary<string, string> missingProperties)
        {
            var value = missingProperties.GetValueOrDefault("Job");
            if (value != null)
                this.JobName = value;
        }
    }
}
