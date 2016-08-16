using System;
using System.IO;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.BuildMaster;
using Inedo.BuildMaster.Artifacts;
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Files;
using Inedo.Diagnostics;
using Inedo.IO;
using Inedo.Extensions.Jenkins;

namespace Inedo.BuildMasterExtensions.Jenkins
{
    internal sealed class JenkinsArtifactImporter
    {
        public string ArtifactName { get; set; }
        public string JobName { get; set; }
        public string BuildNumber { get; set; }

        public IJenkinsConnectionInfo ConnectionInfo { get; }
        public ILogger Logger { get; }
        public IGenericBuildMasterContext Context { get; }

        public JenkinsArtifactImporter(IJenkinsConnectionInfo connectionInfo, ILogger logger, IGenericBuildMasterContext context)
        {
            if (connectionInfo == null)
                throw new ArgumentNullException(nameof(connectionInfo));
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (context.ApplicationId == null)
                throw new InvalidOperationException("context requires a valid application ID");

            this.ConnectionInfo = connectionInfo;
            this.Logger = logger;
            this.Context = context;
        }

        public async Task<string> ImportAsync()
        {
            this.Logger.LogInformation($"Importing artifact \"{this.ArtifactName}\" from Jenkins...");

            string zipFileName = null;
            string jenkinsBuildNumber = await this.ResolveJenkinsBuildNumber().ConfigureAwait(false);
            if (string.IsNullOrEmpty(jenkinsBuildNumber))
            {
                this.Logger.LogError("An error occurred attempting to resolve Jenkins build number \"{0}\". This can mean that "
                    + "the special build type was not found, there are no builds for job \"{1}\", or that the job was not found or is disabled.",
                    this.BuildNumber,
                    this.JobName);

                return null;
            }

            try
            {
                this.Logger.LogInformation("Importing {0} from {1}...", this.ArtifactName, this.JobName);
                var client = new JenkinsClient(this.ConnectionInfo, this.Logger);

                zipFileName = Path.GetTempFileName();
                this.Logger.LogDebug("Temp file: " + zipFileName);

                this.Logger.LogDebug("Downloading artifact...");
                await client.DownloadArtifactAsync(this.JobName, jenkinsBuildNumber, zipFileName).ConfigureAwait(false);
                this.Logger.LogInformation("Artifact downloaded.");

                using (var agent = Util.Agents.CreateLocalAgent())
                {
                    ArtifactBuilder.ImportZip(
                        new ArtifactIdentifier(
                            (int)this.Context.ApplicationId,
                            this.Context.ReleaseNumber,
                            this.Context.BuildNumber,
                            this.Context.DeployableId,
                            TrimWhitespaceAndZipExtension(this.ArtifactName)
                        ),
                        agent.GetService<IFileOperationsExecuter>(),
                        new FileEntryInfo(PathEx.GetFileName(zipFileName), zipFileName)
                    );
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

        private async Task<string> ResolveJenkinsBuildNumber()
        {
            if (AH.ParseInt(this.BuildNumber) != null)
                return this.BuildNumber;

            this.Logger.LogDebug("Build number is not an integer, resolving special build number \"{0}\"...", this.BuildNumber);
            var client = new JenkinsClient(this.ConnectionInfo, this.Logger);
            return await client.GetSpecialBuildNumberAsync(this.JobName, this.BuildNumber).ConfigureAwait(false);
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
