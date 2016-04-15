using System;
using System.ComponentModel;
using System.IO;
using Inedo.BuildMaster;
using Inedo.BuildMaster.Artifacts;
using Inedo.BuildMaster.Data;
using Inedo.BuildMaster.Extensibility.Agents;
using Inedo.BuildMaster.Extensibility.BuildImporters;
using Inedo.BuildMaster.Files;
using Inedo.BuildMaster.Web;
using Inedo.Diagnostics;
using Inedo.Serialization;

namespace Inedo.BuildMasterExtensions.Jenkins
{
    [DisplayName("Jenkins")]
    [Description("Retrieves artifacts from a specific job in Jenkins")]
    [BuildImporterTemplate(typeof(JenkinsBuildImporterTemplate))]
    [CustomEditor(typeof(JenkinsBuildImporterEditor))]
    public sealed class JenkinsBuildImporter : BuildImporterBase, ICustomBuildNumberProvider
    {
        [Persistent]
        public string ArtifactName { get; set; }

        [Persistent]
        public string BuildNumber { get; set; }

        [Persistent]
        public string JobName { get; set; }

        public override void Import(IBuildImporterContext context)
        {
            string zipFileName = null;
            string jenkinsBuildNumber = this.ResolveJenkinsBuildNumber();
            if (string.IsNullOrEmpty(jenkinsBuildNumber))
            {
                this.LogError("An error occurred attempting to resolve Jenkins build number \"{0}\". This can mean that "
                    + "the special build type was not found, there are no builds for job \"{1}\", or that the job was not found or is disabled.",
                    this.BuildNumber,
                    this.JobName);
                return;
            }

            try
            {
                this.LogInformation("Importing {0} from {1}...", this.ArtifactName, this.JobName);
                var client = new JenkinsClient((JenkinsConfigurer)this.GetExtensionConfigurer(), this);

                zipFileName = Path.GetTempFileName();
                this.LogDebug("Temp file: " + zipFileName);

                this.LogDebug("Downloading artifact...");
                client.DownloadArtifact(this.JobName, jenkinsBuildNumber, zipFileName);
                this.LogInformation("Artifact downloaded.");

                using (var agent = Util.Agents.CreateLocalAgent())
                {
                    ArtifactBuilder.ImportZip(
                        new ArtifactIdentifier(
                            context.ApplicationId,
                            context.ReleaseNumber,
                            context.BuildNumber,
                            context.DeployableId,
                            this.ArtifactName),
                        agent.GetService<IFileOperationsExecuter>(),
                        new FileEntryInfo(Path.GetFileName(zipFileName), zipFileName)
                    );
                }
            }
            finally
            {
                try
                {
                    if (zipFileName != null)
                        File.Delete(zipFileName);
                }
                catch (Exception ex)
                {
                    this.LogWarning("Error deleting temp file:" + ex.Message);
                }
            }

            this.LogDebug("Creating $JenkinsBuildNumber variable...");
            DB.Variables_CreateOrUpdateVariableDefinition(
                Variable_Name: "JenkinsBuildNumber",
                Environment_Id: null,
                Server_Id: null,
                ApplicationGroup_Id: null,
                Application_Id: context.ApplicationId,
                Deployable_Id: null,
                Release_Number: context.ReleaseNumber,
                Build_Number: context.BuildNumber,
                Execution_Id: null,
                Promotion_Id: null,
                Value_Text: jenkinsBuildNumber,
                Sensitive_Indicator: false
            );
        }

        private string ResolveJenkinsBuildNumber()
        {
            if (AH.ParseInt(this.BuildNumber) != null)
                return this.BuildNumber;

            this.LogDebug("Build number is not an integer, resolving special build number \"{0}\"...", this.BuildNumber);
            return new JenkinsClient((JenkinsConfigurer)this.GetExtensionConfigurer(), this)
                .GetSpecialBuildNumber(this.JobName, this.BuildNumber);
        }

        string ICustomBuildNumberProvider.BuildNumber
        {
            get
            {
                try
                {
                    return this.ResolveJenkinsBuildNumber();
                }
                catch
                {
                    return this.BuildNumber;
                }
            }
        }
    }
}
