﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Extensibility.Operations;
using Inedo.IO;
using static Inedo.Extensions.Jenkins.InlineIf;

namespace Inedo.Extensions.Jenkins
{
    internal sealed class JenkinsArtifactImporter
    {
        public string ArtifactName { get; set; }
        public string JobName { get; set; }
        public string BranchName { get; set; }
        public string BuildNumber { get; set; }

        public ILogSink Logger { get; }
        private BuildMasterContextShim Context { get; }

        private readonly string username;
        private readonly SecureString password;
        private readonly string serverUrl;
        private readonly bool csrfProtectionEnabled;

        public JenkinsArtifactImporter(string username, SecureString password, string serverUrl, bool csrfProtectionEnabled, ILogSink logger, IOperationExecutionContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            var shim = new BuildMasterContextShim(context);
            if (shim.ApplicationId == null)
                throw new InvalidOperationException("context requires a valid application ID");
            
            this.username = username;
            this.password = password;
            this.serverUrl = serverUrl;
            this.csrfProtectionEnabled = csrfProtectionEnabled;

            this.Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.Context = shim;
        }

        private JenkinsClient CreateClient()
        {
            var client = new JenkinsClient(this.username, this.password, this.serverUrl, this.csrfProtectionEnabled, this.Logger, default);
            return client;
        }

        public async Task<string> ImportAsync()
        {
            string zipFileName = null;
            string jenkinsBuildNumber = await this.ResolveJenkinsBuildNumber().ConfigureAwait(false);
            if (string.IsNullOrEmpty(jenkinsBuildNumber))
            {
                this.Logger.LogError($"An error occurred attempting to resolve Jenkins build number \"{this.BuildNumber}\". "
                   + $"This can mean that the special build type was not found, there are no builds for job \"{this.JobName}\"{IfHasValue(this.BranchName, $" on branch \"{this.BranchName}\"")},"
                   + "or that the job was not found or is disabled."
                );

                return null;
            }

            try
            {
                this.Logger.LogInformation($"Importing artifact from job \"{this.JobName}\"{IfHasValue(this.BranchName, $" on branch \"{this.BranchName}\"")} for build #{jenkinsBuildNumber}...");

                var client = this.CreateClient();

                zipFileName = Path.GetTempFileName();
                this.Logger.LogDebug("Temp file: " + zipFileName);

                this.Logger.LogDebug("Downloading artifact...");
                await client.DownloadArtifactAsync(this.JobName, this.BranchName, jenkinsBuildNumber, zipFileName).ConfigureAwait(false);
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

            this.Logger.LogInformation($"Build number is not an integer, resolving special build number \"{this.BuildNumber}\"...");
            var client = this.CreateClient();
            return client.GetSpecialBuildNumberAsync(this.JobName, this.BranchName, this.BuildNumber);
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
                this.properties = context.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
            public int? ApplicationId => AH.ParseInt(this.GetValue());
            public int? DeployableId => AH.ParseInt(this.GetValue());
            public string ReleaseNumber => this.GetValue();
            public string BuildNumber => this.GetValue();
            public int ExecutionId => this.context.ExecutionId;
            private string GetValue([CallerMemberName] string name = null)
            {
                var prop = this.properties.FirstOrDefault(p => string.Equals(name, p.Name, StringComparison.OrdinalIgnoreCase));
                return prop?.GetValue(this.context)?.ToString();
            }
        }
    }
}
