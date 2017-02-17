using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.IO;
using System;
#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.BuildMaster.Web.Controls;
using Inedo.BuildMaster.Web.Controls.Plans;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Operations;
using Inedo.Otter.Web.Controls;
#endif

namespace Inedo.Extensions.Jenkins.Operations
{
    [DisplayName("Download Jenkins Artifact")]
    [Description("Downloads artifact files from a Jenkins server.")]
    [ScriptNamespace("Jenkins")]
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

        [ScriptAlias("BuildNumber")]
        [DisplayName("Build number")]
        [DefaultValue("lastSuccessfulBuild")]
        [PlaceholderText("lastSuccessfulBuild")]
        [Description("The build number may be a specific build number, or a special value such as \"lastSuccessfulBuild\", \"lastStableBuild\", \"lastBuild\", or \"lastCompletedBuild\".")]
        [SuggestibleValue(typeof(BuildNumberSuggestionProvider))]
        public string BuildNumber { get; set; }

        [ScriptAlias("Artifact")]
        [DisplayName("Artifact name")]
        [PlaceholderText("*")]
        [SuggestibleValue(typeof(ArtifactNameSuggestionProvider))]
        public string ArtifactName { get; set; }

        [ScriptAlias("ExtractFiles")]
        [DisplayName("Extract files")]
        [DefaultValue(true)]
        [Description("Extract archive.zip when downloading all artifacts.")]
        public bool ExtractFilesToTargetDirectory { get; set; } = true;

        [Required]
        [ScriptAlias("TargetDirectory")]
        [DisplayName("Target directory")]
        [Description("The directory to download the artifact to.")]
#if BuildMaster
        [FilePathEditor]
#endif
        public string TargetDirectory { get; set; }

        private JenkinsClient Client => new JenkinsClient(this, this);

        private async Task DownloadZipAsync(IOperationExecutionContext context)
        {
            string targetDirectory = context.ResolvePath(this.TargetDirectory);

            var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false);

            this.LogDebug("Creating remote temporary file...");
            
            using (var tempFile = new RemoteTemporaryFile(fileOps, this))
            {
                this.LogDebug("Downloading artifact to: " + tempFile.Path);

                using (var artifact = await this.Client.OpenArtifactAsync(this.JobName, this.BuildNumber).ConfigureAwait(false))
                using (var tempFileStream = await tempFile.OpenAsync().ConfigureAwait(false))
                {
                    await artifact.Content.CopyToAsync(tempFileStream).ConfigureAwait(false);
                    this.LogDebug("Artifact downloaded.");
                }

                this.LogDebug("Ensuring target directory exists: " + targetDirectory);
                await fileOps.CreateDirectoryAsync(targetDirectory).ConfigureAwait(false);

                if (this.ExtractFilesToTargetDirectory)
                {
                    this.LogDebug("Extracting contents to: " + targetDirectory);
                    await fileOps.ExtractZipFileAsync(tempFile.Path, targetDirectory, true).ConfigureAwait(false);
                    this.LogDebug("Files extracted.");
                }
                else
                {
                    string path = fileOps.CombinePath(targetDirectory, "archive.zip");
                    this.LogDebug("Copying file to: " + path);
                    await fileOps.CopyFileAsync(tempFile.Path, path, true).ConfigureAwait(false);
                    this.LogDebug("File copied.");
                }
            }

            this.LogInformation("Artifact downloaded.");
        }

        private async Task DownloadFileAsync(IOperationExecutionContext context, JenkinsBuildArtifact artifact)
        {
            string targetDirectory = context.ResolvePath(this.TargetDirectory);
            var fileName = PathEx.Combine(targetDirectory, artifact.FileName);
            var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false);

            this.LogDebug("Ensuring target directory exists: " + targetDirectory);
            await fileOps.CreateDirectoryAsync(targetDirectory).ConfigureAwait(false);

            this.LogDebug("Downloading artifact to: " + fileName);

            using (var singleArtifact = await this.Client.OpenSingleArtifactAsync(this.JobName, this.BuildNumber, artifact).ConfigureAwait(false))
            using (var tempFileStream = await fileOps.OpenFileAsync(fileName, FileMode.Create, FileAccess.Write).ConfigureAwait(false))
            {
                await singleArtifact.Content.CopyToAsync(tempFileStream).ConfigureAwait(false);
                this.LogDebug("Artifact downloaded.");
            }
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

                if (this.ExtractFilesToTargetDirectory)
                    this.LogDebug("ExtractFiles option will be ignored because individual file(s) are being downloaded.");

                await Task.WhenAll(filteredArtifacts.Select(artifact => this.DownloadFileAsync(context, artifact))).ConfigureAwait(false);

                this.LogInformation($"{filteredArtifacts.Count} artifact(s) downloaded.");
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

        private sealed class RemoteTemporaryFile : IDisposable
        {
            private IFileOperationsExecuter fileOps;
            private ILogger log;

            public RemoteTemporaryFile(IFileOperationsExecuter fileOps, ILogger log)
            {
                this.fileOps = fileOps;
                this.log = log;

                string workingDirectory = fileOps.GetBaseWorkingDirectory();
                string fileName = Guid.NewGuid().ToString("n");

                this.Path = fileOps.CombinePath(workingDirectory, fileName);
            }

            public string Path { get; }

            public Task<Stream> OpenAsync()
            {
                return this.fileOps.OpenFileAsync(this.Path, FileMode.Create, FileAccess.Write);
            }

            public void Dispose()
            {
                this.log.LogDebug("Deleting temp file: " + this.Path);
                try
                {
                    this.fileOps.DeleteFile(this.Path);
                }
                catch (Exception ex)
                {
                    this.log.LogWarning("Temp file could not be deleted: " + ex.Message);
                }
            }
        }
    }
}
