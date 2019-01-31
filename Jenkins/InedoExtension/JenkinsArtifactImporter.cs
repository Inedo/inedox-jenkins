using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Extensibility.Operations;
using Inedo.IO;

namespace Inedo.Extensions.Jenkins
{
    internal sealed class JenkinsArtifactImporter
    {
        public string ArtifactName { get; set; }
        public string JobName { get; set; }
        public string BuildNumber { get; set; }
        public string SubFolder { get; set; }

        public IJenkinsConnectionInfo ConnectionInfo { get; }
        public ILogSink Logger { get; }
        private BuildMasterContextShim Context { get; }

        public JenkinsArtifactImporter(IJenkinsConnectionInfo connectionInfo, ILogSink logger, IOperationExecutionContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            var shim = new BuildMasterContextShim(context);
            if (shim.ApplicationId == null)
                throw new InvalidOperationException("context requires a valid application ID");

            ConnectionInfo = connectionInfo ?? throw new ArgumentNullException(nameof(connectionInfo));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Context = shim;
        }

        public async Task<string> ImportAsync()
        {
            Logger.LogInformation($"Importing artifact \"{ArtifactName}\" from Jenkins...");

            string zipFileName = null;
            string jenkinsBuildNumber = await ResolveJenkinsBuildNumber().ConfigureAwait(false);
            if (string.IsNullOrEmpty(jenkinsBuildNumber))
            {
                Logger.LogError($"An error occurred attempting to resolve Jenkins build number \"{BuildNumber}\". "
                   + $"This can mean that the special build type was not found, there are no builds for job \"{JobName}\", "
                   +"or that the job was not found or is disabled."
                );

                return null;
            }

            try
            {
                Logger.LogInformation($"Importing {ArtifactName} from {JobName}...");
                var client = new JenkinsClient(ConnectionInfo, Logger);

                zipFileName = Path.GetTempFileName();
                Logger.LogDebug("Temp file: " + zipFileName);

                Logger.LogDebug("Downloading artifact...");
                await client.DownloadArtifactAsync(JobName, jenkinsBuildNumber, zipFileName, SubFolder).ConfigureAwait(false);
                Logger.LogInformation("Artifact downloaded.");

                using (var file = FileEx.Open(zipFileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    await SDK.CreateArtifactAsync(
                        (int)Context.ApplicationId,
                        Context.ReleaseNumber,
                        Context.BuildNumber,
                        Context.DeployableId,
                        Context.ExecutionId,
                        TrimWhitespaceAndZipExtension(ArtifactName),
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
                        Logger.LogDebug("Removing temp file...");
                        FileEx.Delete(zipFileName);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning("Error deleting temp file:" + ex.Message);
                }
            }

            Logger.LogInformation(ArtifactName + " artifact imported.");

            return jenkinsBuildNumber;
        }

        private Task<string> ResolveJenkinsBuildNumber()
        {
            if (AH.ParseInt(BuildNumber) != null)
                return Task.FromResult(BuildNumber);

            Logger.LogDebug($"Build number is not an integer, resolving special build number \"{BuildNumber}\"...");
            var client = new JenkinsClient(ConnectionInfo, Logger);
            return client.GetSpecialBuildNumberAsync(JobName, BuildNumber);
        }

        private static string TrimWhitespaceAndZipExtension(string artifactName)
        {
            string file = PathEx.GetFileName(artifactName).Trim();
            if (file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                return file.Substring(0, file.Length - ".zip".Length);
            else
                return file;
        }

        private sealed class BuildMasterContextShim
        {
            private readonly IOperationExecutionContext context;
            private readonly PropertyInfo[] properties;
            public BuildMasterContextShim(IOperationExecutionContext context)
            {
                // this is absolutely horrid, but works for backwards compatibility since this can only be used in BuildMaster
                this.context = context;
                properties = context.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
            public int? ApplicationId => AH.ParseInt(GetValue());
            public int? DeployableId => AH.ParseInt(GetValue());
            public string ReleaseNumber => GetValue();
            public string BuildNumber => GetValue();
            public int ExecutionId => context.ExecutionId;
            private string GetValue([CallerMemberName] string name = null)
            {
                var prop = properties.FirstOrDefault(p => string.Equals(name, p.Name, StringComparison.OrdinalIgnoreCase));
                return prop?.GetValue(context)?.ToString();
            }
        }
    }
}
