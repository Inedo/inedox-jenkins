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
        [ScriptAlias("Credentials")]
        [DisplayName("Credentials")]
        public override string CredentialName { get; set; }

        [Required]
        [ScriptAlias("Job")]
        [DisplayName("Job name")]
        [SuggestableValue(typeof(JobNameSuggestionProvider))]
        public string JobName { get; set; }

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
        [Description("The name of the artifact in BuildMaster once it is captured from the {jenkinsUrl}/job/{jobName}/{buildNumber}/artifact/*zip*/archive.zip endpoint.")]
        public string ArtifactName { get; set; }

        [Output]
        [ScriptAlias("JenkinsBuildNumber")]
        [DisplayName("Set build number to variable")]
        [Description("The Jenkins build number can be output into a runtime variable.")]
        [PlaceholderText("e.g. $JenkinsBuildNumber")]
        public string JenkinsBuildNumber { get; set; }

        [ScriptAlias("SubFolder")]
        [DisplayName("Sub Folder name")]
        [Description("Optional: The name of the subfolder artifacts to retrieve from Jenkins instead of all the Jenkins Artifact contents i.e: 'output/image' to get image.zip from .../artifact/output/image/*zip*/image.zip instead of .../archive.zip")]
        public string SubFolder { get; set; }

        public async override Task ExecuteAsync(IOperationExecutionContext context)
        {
            var importer = new JenkinsArtifactImporter(this, this, context)
            {
                ArtifactName = ArtifactName,
                BuildNumber = BuildNumber,
                JobName = JobName,
                SubFolder = SubFolder
            };

            JenkinsBuildNumber = await importer.ImportAsync().ConfigureAwait(false);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            string buildNumber = config[nameof(BuildNumber)];

            return new ExtendedRichDescription(
                new RichDescription("Import Jenkins ", new Hilite(config[nameof(ArtifactName)]), " Artifact "),
                new RichDescription("of build ",
                    AH.ParseInt(buildNumber) != null ? "#" : "",
                    new Hilite(buildNumber),
                    " for job ",
                    new Hilite(config[nameof(JobName)])
                )
            );
        }
    }
}
