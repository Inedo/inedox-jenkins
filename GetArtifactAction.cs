using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Inedo.BuildMaster;
using Inedo.BuildMaster.Extensibility.Actions;
using Inedo.BuildMaster.Extensibility.Agents;
using Inedo.BuildMaster.Web;

namespace Inedo.BuildMasterExtensions.Jenkins
{
    [ActionProperties(
        "Download Jenkins Artifact",
        "Downloads artifact files from a Jenkins server.",
        DefaultToLocalServer = true)]
    [RequiresInterface(typeof(IFileOperationsExecuter))]
    [RequiresInterface(typeof(IRemoteZip))]
    [CustomEditor(typeof(GetArtifactActionEditor))]
    [Tag("jenkins")]
    public sealed class GetArtifactAction : AgentBasedActionBase
    {
        [Persistent]
        public string JobName { get; set; }

        [Persistent]
        public string ArtifactName { get; set; }

        [Persistent]
        public string BuildNumber { get; set; }

        [Persistent]
        public bool ExtractFilesToTargetDirectory { get; set; }

        public GetArtifactAction()
        {
            this.ExtractFilesToTargetDirectory = true;
        }

        public override ActionDescription GetActionDescription()
        {
            return new ActionDescription(
                new ShortActionDescription("Download ", new Hilite(this.JobName), " Artifact"),
                new LongActionDescription(
                    this.ExtractFilesToTargetDirectory ? "" : "as zip file ",
                    "from Jenkins to ", new DirectoryHilite(this.OverriddenTargetDirectory))
            );
        }

        private JenkinsClient getClient()
        {
            return new JenkinsClient((JenkinsConfigurer)this.GetExtensionConfigurer());
        }

        
        private void DownloadZip()
        {
            var config = (JenkinsConfigurer)this.GetExtensionConfigurer();

            var remoteFileName = this.ExtractFilesToTargetDirectory
                ? Util.Path2.Combine(this.Context.TempDirectory, "archive.zip")
                : Util.Path2.Combine(this.Context.TargetDirectory, "archive.zip");
            
            var fileOps = this.Context.Agent.GetService<IFileOperationsExecuter>();
            var remote = this.Context.Agent.GetService<IRemoteMethodExecuter>();
            var zip = this.Context.Agent.GetService<IRemoteZip>();

            if (remote != null)
            {
                this.LogDebug("Downloading to {0}...", remoteFileName);
                remote.InvokeAction<JenkinsConfigurer, string, string, string>(
                    (cfg, job, bld, fil) => new JenkinsClient(cfg).DownloafArtifact(job, bld, fil),
                    (JenkinsConfigurer)this.GetExtensionConfigurer(), this.JobName, this.BuildNumber, remoteFileName);
            }
            else
            {
                var localFileName = Path.GetTempFileName();

                this.LogDebug("Downloading to {0}...", localFileName);
                new JenkinsClient(config).DownloafArtifact(this.JobName, this.BuildNumber, localFileName);

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
                zip.ExtractZipFile(remoteFileName, this.Context.TargetDirectory, true);
            }

            this.LogInformation("Artifact successfully downloaded.");
        }

        private void DownloadFile(JenkinsBuildArtifact artifact)
        {
            var config = (JenkinsConfigurer)this.GetExtensionConfigurer();

            var remoteFileName = Util.Path2.Combine(this.Context.TargetDirectory, artifact.relativePath);
            var fileOps = this.Context.Agent.GetService<IFileOperationsExecuter>();
            var remote = this.Context.Agent.GetService<IRemoteMethodExecuter>();
            var zip = this.Context.Agent.GetService<IRemoteZip>();

            if (remote != null)
            {
                this.LogDebug("Downloading to {0}...", remoteFileName);
                remote.InvokeAction<JenkinsConfigurer, string, string, string>(
                    (cfg, job, bld, fil) => new JenkinsClient(cfg).DownloafSingleArtifact(job, bld, fil, artifact),
                    (JenkinsConfigurer)this.GetExtensionConfigurer(), this.JobName, this.BuildNumber, remoteFileName);
            }
            else
            {
                var localFileName = Path.GetTempFileName();

                this.LogDebug("Downloading to {0}...", localFileName);
                new JenkinsClient(config).DownloafSingleArtifact(this.JobName, this.BuildNumber, localFileName, artifact);

                this.LogDebug("Transferring to server...", remoteFileName);
                using (var localFile = File.OpenRead(localFileName))
                using (var remoteFile = fileOps.OpenFile(remoteFileName, FileMode.Create, FileAccess.Write))
                {
                    localFile.CopyTo(remoteFile);
                }
            }

        }
        
        protected override void Execute()
        {
            if (InedoLib.Util.Int.ParseN(this.BuildNumber) == null)
            {
                var client = getClient();
                
                this.LogDebug("Looking up {0}...", this.BuildNumber);
                this.BuildNumber = client.GetSpecialBuildNumber(this.JobName, this.BuildNumber);
                this.LogInformation("Using Jenkins build number {0}.", this.BuildNumber);
            }

            if (string.IsNullOrEmpty(this.ArtifactName) || this.ArtifactName == "*")
            {
                this.LogDebug("Artifact filter not specified; downloading all artifacts as zip...");
                this.DownloadZip();
            }
            else
            {
                var client = getClient();
                
                var artifacts = client.GetBuildArtifacts(this.JobName, this.BuildNumber);
                this.LogDebug("Build contains {0} build artifacts.", artifacts.Count);
                if (artifacts.Count == 0)
                {
                    this.LogWarning("Build contains no artifacts");
                    return;
                }

                var pattern = "^" + Regex.Escape(this.ArtifactName)
                    .Replace(@"\*",".*")
                    .Replace(@"\?",".") + "$";

                var filteredArtifacts = artifacts
                    .Where(a => Regex.IsMatch(a.relativePath, pattern))
                    .ToList();
                this.LogDebug("{0} artifacts match pattern \"{1}\".", filteredArtifacts.Count, pattern);
                if (filteredArtifacts.Count == 0)
                {
                    this.LogWarning("Build contains no filtered artifacts");
                    return;
                }

                if (!this.ExtractFilesToTargetDirectory)
                    this.LogWarning("ExtractFilesToTargetDirectory will be ignored, as individual file(s) are being downloaded.");

                foreach (var artifact in artifacts)
                    this.DownloadFile(artifact);
            }
        }

    }
}
