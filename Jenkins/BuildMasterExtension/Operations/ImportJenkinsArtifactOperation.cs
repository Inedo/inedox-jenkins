using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.BuildMaster.Data;
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.BuildMaster.Web.Controls;
using Inedo.BuildMasterExtensions.Jenkins;
using Inedo.Diagnostics;
using Inedo.Documentation;

namespace Inedo.Extensions.Jenkins.Operations
{
    [DisplayName("Import Artifact from Jenkins")]
    [Description("Downloads an artifact from the specified Jenkins server and saves it to the artifact library.")]
    [ScriptAlias("Import-Artifact")]
    [Tag("artifacts")]
    [Tag("jenkins")]
    public sealed class ImportJenkinsArtifactOperation : JenkinsOperation
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

        [Required]
        [ScriptAlias("Artifact")]
        [DisplayName("Artifact name")]
        [Description("The name of the artifact in BuildMaster once it is captured from the {jenkinsUrl}/job/{jobName}/{buildNumber}/artifact/*zip*/archive.zip endpoint.")]
        public string ArtifactName { get; set; }

        public async override Task ExecuteAsync(IOperationExecutionContext context)
        {
            var importer = new JenkinsArtifactImporter((IJenkinsConnectionInfo)this, (ILogger)this, context)
            {
                ArtifactName = this.ArtifactName,
                BuildNumber = this.BuildNumber,
                JobName = this.JobName
            };

            string jenkinsBuildNumber = await importer.ImportAsync().ConfigureAwait(false);

            if (jenkinsBuildNumber != null)
            {
                this.LogDebug("Creating $JenkinsBuildNumber variable...");
                await new DB.Context(false).Variables_CreateOrUpdateVariableDefinitionAsync(
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
                ).ConfigureAwait(false);
            }
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            string buildNumber = config[nameof(this.BuildNumber)];

            return new ExtendedRichDescription(
                new RichDescription("Import Jenkins ", new Hilite(config[nameof(this.ArtifactName)]), " Artifact "),
                new RichDescription("of build ",
                    AH.ParseInt(buildNumber) != null ? "#" : "",
                    new Hilite(buildNumber),
                    " for job ",
                    new Hilite(config[nameof(this.JobName)])
                )
            );
        }
    }
}
