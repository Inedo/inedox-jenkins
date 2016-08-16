using System.ComponentModel;
#if BuildMaster
using Inedo.BuildMaster.Data;
using Inedo.BuildMaster.Extensibility.BuildImporters;
using Inedo.BuildMaster.Web;
#elif Otter
using Inedo.Otter.Data;
using Inedo.Otter.Extensibility.BuildImporters;
using Inedo.Otter.Web;
#endif
using Inedo.Diagnostics;
using Inedo.Serialization;

namespace Inedo.Extensions.Jenkins
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
            var configurer = (JenkinsConfigurer)this.GetExtensionConfigurer();
            var importer = new JenkinsArtifactImporter(configurer, this, context)
            {
                ArtifactName = this.ArtifactName,
                BuildNumber = this.BuildNumber,
                JobName = this.JobName
            };

            string jenkinsBuildNumber = importer.ImportAsync().Result();

            if (jenkinsBuildNumber != null)
            {
                this.LogDebug("Creating $JenkinsBuildNumber variable...");
                DB.Variables_CreateOrUpdateVariableDefinition(
                    Variable_Name: "JenkinsBuildNumber",
                    Environment_Id: null,
                    ServerRole_Id: null,
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
        }

        private string ResolveJenkinsBuildNumber()
        {
            if (AH.ParseInt(this.BuildNumber) != null)
                return this.BuildNumber;

            this.LogDebug("Build number is not an integer, resolving special build number \"{0}\"...", this.BuildNumber);
            return new JenkinsClient((JenkinsConfigurer)this.GetExtensionConfigurer(), this)
                .GetSpecialBuildNumberAsync(this.JobName, this.BuildNumber).Result();
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
