using System;
using System.IO;
using Inedo.BuildMaster;
using Inedo.BuildMaster.Artifacts;
using Inedo.BuildMaster.Data;
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Agents;
using Inedo.BuildMaster.Extensibility.BuildImporters;
using Inedo.BuildMaster.Files;
using Inedo.BuildMaster.Web;
using Inedo.Data;

namespace Inedo.BuildMasterExtensions.Jenkins
{
    [BuildImporterProperties("Jenkins",
        "Retrieves artifacts from a specific job in Jenkins",
        typeof(JenkinsBuildImporterTemplate))]
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
            try
            {
                this.LogDebug("Importing {0} from {1}...", this.ArtifactName, this.JobName);
                var client = new JenkinsClient((JenkinsConfigurer)this.GetExtensionConfigurer());

                zipFileName = Path.GetTempFileName();
                this.LogDebug("Temp file: " + zipFileName);

                this.LogDebug("Downloading artifact...");
                client.DownloadArtifact(this.JobName, this.BuildNumber, zipFileName);
                this.LogDebug("Artifact downloaded.");

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
            StoredProcs.Variables_CreateOrUpdateVariableDefinition(
                Variable_Name: "JenkinsBuildNumber", 
                Environment_Id: null, 
                Server_Id: null, 
                ApplicationGroup_Id: null,
                Application_Id: context.ApplicationId, 
                Deployable_Id: null, 
                Release_Number: context.ReleaseNumber, 
                Build_Number: context.BuildNumber,
                Execution_Id: null,
                Value_Text: this.BuildNumber,
                Sensitive_Indicator: YNIndicator.No).Execute();
        }
    }
}
