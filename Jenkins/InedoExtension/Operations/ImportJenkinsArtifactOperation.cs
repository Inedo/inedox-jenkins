using static Inedo.Extensions.Jenkins.InlineIf;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Web;

namespace Inedo.Extensions.Jenkins.Operations
{
    [DisplayName("Import Artifact from Jenkins")]
    [Description("Downloads an artifact from the specified Jenkins server and saves it to the artifact library.")]
    [ScriptAlias("Import-Artifact")]
    [Tag("artifacts")]
    [Tag("jenkins")]
    [AppliesTo(InedoProduct.BuildMaster)]
    public sealed class ImportJenkinsArtifactOperation : JenkinsOperation
    {
        [Required]
        [ScriptAlias("Job")]
        [DisplayName("Job name")]
        [SuggestableValue(typeof(JobNameSuggestionProvider))]
        public string JobName { get; set; }

        [ScriptAlias("Branch")]
        [DisplayName("Branch name")]
        [SuggestableValue(typeof(BranchNameSuggestionProvider))]
        [Description("The branch name is required for a Jenkins multi-branch project, otherwise should be left empty.")]
        public string BranchName { get; set; }

        [ScriptAlias("BuildNumber")]
        [DisplayName("Build number")]
        [DefaultValue("lastSuccessfulBuild")]
        [PlaceholderText("lastSuccessfulBuild")]
        [Description("The build number may be a specific build number, or a special value such as \"lastSuccessfulBuild\", \"lastStableBuild\", \"lastBuild\", or \"lastCompletedBuild\".")]
        [SuggestableValue(typeof(BuildNumberSuggestionProvider))]
        public string BuildNumber { get; set; }

        [Required]
        [ScriptAlias("Artifact")]
        [DisplayName("Artifact name")]
        [PlaceholderText("e.g. archive.zip")]
        [Description("The name of the artifact in BuildMaster once it is captured from the {jenkinsUrl}/job/{jobName}/{buildNumber}/artifact/*zip*/archive.zip endpoint.")]
        public string ArtifactName { get; set; }

        [Output]
        [ScriptAlias("JenkinsBuildNumber")]
        [DisplayName("Set build number to variable")]
        [Description("The Jenkins build number can be output into a runtime variable.")]
        [PlaceholderText("e.g. $JenkinsBuildNumber")]
        public string JenkinsBuildNumber { get; set; }

        public async override Task ExecuteAsync(IOperationExecutionContext context)
        {
            var (c, r) = this.GetCredentialsAndResource(context);
            var user = (c as Extensions.Credentials.UsernamePasswordCredentials)?.UserName;
            var pass = (c as Extensions.Credentials.UsernamePasswordCredentials)?.Password ?? (c as Extensions.Credentials.TokenCredentials)?.Token;

            var importer = new JenkinsArtifactImporter(user, pass, r.ServerUrl, false, this, context)
            {
                ArtifactName = this.ArtifactName,
                BuildNumber = this.BuildNumber,
                BranchName = this.BranchName,
                JobName = this.JobName
            };

            this.JenkinsBuildNumber = await importer.ImportAsync().ConfigureAwait(false);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            string jobName = config[nameof(this.JobName)];
            string artifactName = config[nameof(this.ArtifactName)];

            return new ExtendedRichDescription(
                new RichDescription("Import Jenkins Artifact ", new Hilite(artifactName)),
                new RichDescription("from job ", new Hilite(jobName))
            );
        }
    }
}
