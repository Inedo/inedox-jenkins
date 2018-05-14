using System;
using System.IO;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.IO;

namespace Inedo.Extensions.Jenkins
{
    internal sealed class JenkinsArtifactImporter
    {
        public string ArtifactName { get; set; }
        public string JobName { get; set; }
        public string BuildNumber { get; set; }

        public IJenkinsConnectionInfo ConnectionInfo { get; }
        public ILogSink Logger { get; }
        public dynamic Context { get; }

        public JenkinsArtifactImporter(IJenkinsConnectionInfo connectionInfo, ILogSink logger, dynamic context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (context.ApplicationId == null)
                throw new InvalidOperationException("context requires a valid application ID");

            this.ConnectionInfo = connectionInfo ?? throw new ArgumentNullException(nameof(connectionInfo));
            this.Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.Context = context;
        }

        public async Task<string> ImportAsync()
        {
            this.Logger.LogInformation($"Importing artifact \"{this.ArtifactName}\" from Jenkins...");

            string zipFileName = null;
            string jenkinsBuildNumber = await this.ResolveJenkinsBuildNumber().ConfigureAwait(false);
            if (string.IsNullOrEmpty(jenkinsBuildNumber))
            {
                this.Logger.LogError($"An error occurred attempting to resolve Jenkins build number \"{this.BuildNumber}\". "
                   + $"This can mean that the special build type was not found, there are no builds for job \"{this.JobName}\", "
                   +"or that the job was not found or is disabled."
                );

                return null;
            }

            try
            {
                this.Logger.LogInformation($"Importing {this.ArtifactName} from {this.JobName}...");
                var client = new JenkinsClient(this.ConnectionInfo, this.Logger);

                zipFileName = Path.GetTempFileName();
                this.Logger.LogDebug("Temp file: " + zipFileName);

                this.Logger.LogDebug("Downloading artifact...");
                await client.DownloadArtifactAsync(this.JobName, jenkinsBuildNumber, zipFileName).ConfigureAwait(false);
                this.Logger.LogInformation("Artifact downloaded.");

                using (var file = FileEx.Open(zipFileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    await SDK.CreateArtifactAsync(
                        (int)this.Context.ApplicationId,
                        this.Context.ReleaseNumber,
                        this.Context.BuildNumber,
                        this.Context.DeployableId,
                        this.Context.ExecutionId,
                        TrimWhitespaceAndZipExtension(this.ArtifactName),
                        file,
                        true
                    ).ConfigureAwait(false);
                }
            }
            finally
            {
                try
                {
                    if (zipFileName != null)
                    {
                        this.Logger.LogDebug("Removing temp file...");
                        FileEx.Delete(zipFileName);
                    }
                }
                catch (Exception ex)
                {
                    this.Logger.LogWarning("Error deleting temp file:" + ex.Message);
                }
            }

            this.Logger.LogInformation(this.ArtifactName + " artifact imported.");

            return jenkinsBuildNumber;
        }

        private Task<string> ResolveJenkinsBuildNumber()
        {
            if (AH.ParseInt(this.BuildNumber) != null)
                return Task.FromResult(this.BuildNumber);

            this.Logger.LogDebug($"Build number is not an integer, resolving special build number \"{this.BuildNumber}\"...");
            var client = new JenkinsClient(this.ConnectionInfo, this.Logger);
            return client.GetSpecialBuildNumberAsync(this.JobName, this.BuildNumber);
        }

        private static string TrimWhitespaceAndZipExtension(string artifactName)
        {
            string file = PathEx.GetFileName(artifactName).Trim();
            if (file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                return file.Substring(0, file.Length - ".zip".Length);
            else
                return file;
        }
    }
}
