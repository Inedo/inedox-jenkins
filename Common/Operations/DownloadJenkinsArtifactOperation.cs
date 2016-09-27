using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.IO;
using Inedo.Serialization;
#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.BuildMaster.Web.Controls;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Operations;
using Inedo.Otter.Web.Controls;
#endif

namespace Inedo.Extensions.Jenkins.Operations
{
    [DisplayName("Download Jenkins Artifact")]
    [Description("Downloads artifact files from a Jenkins server.")]
    [ScriptAlias("Download-Artifact")]
    [Tag("jenkins")]
    [Tag("artifacts")]
    public sealed class DownloadJenkinsArtifactOperation : JenkinsOperation
    {
        [ScriptAlias("Credentials")]
        [DisplayName("Credentials")]
        public override string CredentialName { get; set; }

        [Required]
        [ScriptAlias("Job")]
        [DisplayName("Job name")]
        [SuggestibleValue(typeof(JobNameSuggestionProvider))]
        public string JobName { get; set; }

        [Required]
        [ScriptAlias("Artifact")]
        [DisplayName("Artifact name")]
        [SuggestibleValue(typeof(ArtifactNameSuggestionProvider))]
        public string ArtifactName { get; set; }

        [ScriptAlias("BuildNumber")]
        [DisplayName("Build number")]
        [DefaultValue("lastSuccessfulBuild")]
        [PlaceholderText("lastSuccessfulBuild")]
        [Description("The build number may be a specific build number, or a special value such as \"lastSuccessfulBuild\", \"lastStableBuild\", \"lastBuild\", or \"lastCompletedBuild\".")]
        public string BuildNumber { get; set; }

        [Persistent]
        [ScriptAlias("ExtractFiles")]
        [DisplayName("Extract files")]
        [DefaultValue(true)]
        [Description("Extract archive.zip when downloading all artifacts")]
        public bool ExtractFilesToTargetDirectory { get; set; } = true;

        [Persistent]
        [Required]
        [ScriptAlias("TargetDirectory")]
        [DisplayName("Target directory")]
        [Description("The directory to download the artifact to")]
        public string TargetDirectory { get; set; }

        private JenkinsClient Client => new JenkinsClient(this, this);

        private async Task DownloadZipAsync(IOperationExecutionContext context)
        {
            var fileName = this.ExtractFilesToTargetDirectory
                ? Path.GetTempFileName()
                : PathEx.Combine(this.TargetDirectory, "archive.zip");

            var fileOps = context.Agent.GetService<IFileOperationsExecuter>();

            this.LogDebug("Downloading to {0}...", fileName);
            await this.Client.DownloadArtifactAsync(this.JobName, this.BuildNumber, fileName).ConfigureAwait(false);

            if (this.ExtractFilesToTargetDirectory)
            {
                this.LogDebug("Extracting to {0}...", this.TargetDirectory);
                await fileOps.ExtractZipFileAsync(fileName, this.TargetDirectory, true).ConfigureAwait(false);
                await fileOps.DeleteFileAsync(fileName).ConfigureAwait(false);
            }

            this.LogInformation("Artifact successfully downloaded.");
        }

        private async Task DownloadFileAsync(IOperationExecutionContext context, JenkinsBuildArtifact artifact)
        {
            var fileName = PathEx.Combine(this.TargetDirectory, artifact.FileName);
            var fileOps = context.Agent.GetService<IFileOperationsExecuter>();

            this.LogDebug("Downloading to {0}...", fileName);
            await this.Client.DownloadSingleArtifactAsync(this.JobName, this.BuildNumber, fileName, artifact).ConfigureAwait(false);
        }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            if (AH.ParseInt(this.BuildNumber) == null)
            {
                this.LogDebug("Looking up {0}...", this.BuildNumber);
                this.BuildNumber = await this.Client.GetSpecialBuildNumberAsync(this.JobName, this.BuildNumber).ConfigureAwait(false);
                this.LogInformation("Using Jenkins build number {0}.", this.BuildNumber);
            }

            if (string.IsNullOrEmpty(this.ArtifactName) || this.ArtifactName == "*")
            {
                this.LogDebug("Artifact filter not specified; downloading all artifacts as zip...");
                await this.DownloadZipAsync(context);
            }
            else
            {
                var artifacts = await this.Client.GetBuildArtifactsAsync(this.JobName, this.BuildNumber).ConfigureAwait(false);
                this.LogDebug("Build contains {0} build artifacts.", artifacts.Count);
                if (artifacts.Count == 0)
                {
                    this.LogWarning("Build contains no artifacts");
                    return;
                }

                foreach (var artifact in artifacts)
                    this.LogDebug("Found artifact: (fileName=\"{0}\", relativePath=\"{1}\", displayPath=\"{2}\")", artifact.FileName, artifact.RelativePath, artifact.DisplayPath);

                var pattern = "^" + Regex.Escape(this.ArtifactName)
                    .Replace(@"\*", ".*")
                    .Replace(@"\?", ".") + "$";

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

                await Task.WhenAll(filteredArtifacts.Select(artifact => this.DownloadFileAsync(context, artifact))).ConfigureAwait(false);
            }
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription("Download ", new Hilite(config[nameof(this.JobName)]), " Artifact"),
                new RichDescription(
                    config[nameof(this.ExtractFilesToTargetDirectory)] == bool.TrueString ? "" : "as zip file ",
                    "from Jenkins to ", new DirectoryHilite(config[nameof(this.TargetDirectory)])
                )
            );
        }
    }
}
