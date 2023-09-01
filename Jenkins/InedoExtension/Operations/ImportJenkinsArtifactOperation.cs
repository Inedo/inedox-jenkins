using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Reflection;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Jenkins.Credentials;
using Inedo.IO;
using Inedo.Web;

namespace Inedo.Extensions.Jenkins.Operations;

[Description("Downloads an artifact from the specified Jenkins server and saves it to the artifact library.")]
[ScriptAlias("Import-Artifacts")]
[ScriptAlias("Import-Artifact", Obsolete = true)]
[AppliesTo(InedoProduct.BuildMaster)]
public sealed class ImportJenkinsArtifactsOperation : JenkinsOperation, IImportCIArtifactsOperation
{
    [ScriptAlias("From")]
    [DisplayName("Jenkins resource")]
    [DefaultValue("$CIProject")]
    [SuggestableValue(typeof(SecureResourceSuggestionProvider<JenkinsProject>))]
    public override string? ResourceName { get; set; }
    [ScriptAlias("Project"), ScriptAlias("Job", Obsolete = true)]
    [DisplayName("Project name")]
    [DefaultValue("$JenkinsProjectName($CIProject)")]
    [SuggestableValue(typeof(ProjectNameSuggestionProvider))]
    public override string? ProjectName { get; set; }
    [ScriptAlias("Branch")]
    [DisplayName("Branch name")]
    [DefaultValue("$JenkinsBranchName($CIBuild)")]
    [SuggestableValue(typeof(BranchNameSuggestionProvider))]
    [Description("The branch name is required for a Jenkins multi-branch project, otherwise should be left empty.")]
    public override string? BranchName { get; set; }
    [ScriptAlias("BuildNumber")]
    [DisplayName("Build number")]
    [DefaultValue("$JenkinsBuildNumber($CIBuild)")]
    [Description("The build number may be a specific build number, or a special value such as \"lastSuccessfulBuild\", \"lastStableBuild\", \"lastBuild\", or \"lastCompletedBuild\".")]
    [SuggestableValue(typeof(BuildNumberSuggestionProvider))]
    public string? BuildNumber { get; set; }

    [Category("Advanced")]
    [ScriptAlias("Artifact")]
    [DisplayName("BuildMaster artifact name")]
    [DefaultValue("Default"), NotNull]
    [Description("The name of the artifact in BuildMaster to create after artifacts are downloaded from Jenkins.")]
    public string? ArtifactName { get; set; }
    [Category("Advanced")]
    [ScriptAlias("Include")]
    [DisplayName("Include files")]
    [DefaultValue("**")]
    [Description(CommonDescriptions.MaskingHelp)]
    public IEnumerable<string>? Includes { get; set; }
    [Category("Advanced")]
    [ScriptAlias("Exclude")]
    [DisplayName("Exclude files")]
    [Description(CommonDescriptions.MaskingHelp)]
    public IEnumerable<string>? Excludes { get; set; }
    [Output]
    [Category("Advanced")]
    [ScriptAlias("JenkinsBuildNumber")]
    [DisplayName("Actual build number (output)")]
    [PlaceholderText("e.g. $ActualBuildNumber")]
    [Description("When you specify a Build Number like \"lastBuild\", this will output the real Jenkins BuildNumber into a runtime variable.")]
    public string? JenkinsBuildNumber { get; set; }

    string? IImportCIArtifactsOperation.BuildId
    {
        get => AH.NullIf(this.BranchName + "-", "-") + this.BuildNumber;
        set
        {
            if (value == null)
            {
                this.BranchName = null;
                this.BuildNumber = null;
            }
            else
            {
                JenkinsClient.ParseBuildId(value, out var branch, out var number);
                this.BranchName = branch;
                this.BuildNumber = number.ToString();
            }

        }
    }

    public async override Task ExecuteAsync(IOperationExecutionContext context)
    {
        if (this.ProjectName == null)
            throw new ExecutionFailureException($"No Jenkins project was specified, and there is no CI build associated with this execution.");
        if (this.BuildNumber == null)
            throw new ExecutionFailureException($"No Jenkins build was specified, and there is no CI build associated with this execution.");
        if (!this.TryCreateClient(context, out var client))
            throw new ExecutionFailureException($"Could not create a connection to Jenkins resource \"{AH.CoalesceString(this.ResourceName,this.ServerUrl)}\".");

        int actualBuildNumber = await client.GetActualBuildNumber(this.ProjectName, this.BranchName, this.BuildNumber, context.CancellationToken);
        this.JenkinsBuildNumber = actualBuildNumber.ToString();

        var count = 0;
        using var tempStream = new TemporaryStream();
        using (var zip = new ZipArchive(tempStream, ZipArchiveMode.Create, true))
        {
            var mask = new MaskingContext(this.Includes, this.Excludes);

            await foreach (var a in client.GetBuildArtifactsAsync(this.ProjectName, this.BranchName, actualBuildNumber, context.CancellationToken))
            {
                if (mask.IsMatch(a))
                {
                    count++;
                    var entry = zip.CreateEntry(a);
                    using var entryStream = entry.Open();
                    await client.DownloadArtifactAsync(this.ProjectName, this.BranchName, actualBuildNumber, a, entryStream, context.CancellationToken);
                }
            }
        }

        tempStream.Position = 0;
        if (count == 0)
            this.LogWarning("No artifacts were downloaded from Jenkins.");
        else
            this.LogInformation($"{count} artifacts were downloaded from Jenkins.");

        await context.CreateBuildMasterArtifactAsync(this.ArtifactName, tempStream, true, context.CancellationToken);
        this.LogInformation($"{this.ArtifactName} artifact created in BuildMaster.");
    }

    protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
    {
        string? val(string name) => AH.NullIf(config[name], this.GetType().GetProperty(name)?.GetCustomAttribute<DefaultValueAttribute>()?.Value?.ToString());

        var projectName = val(nameof(this.ProjectName));
        var branchName = val(nameof(this.BranchName));
        var buildNum = val(nameof(this.BuildNumber));

        if (!string.IsNullOrEmpty(branchName))
            projectName += $" (Branch ${branchName}";
            

        return new ExtendedRichDescription(
            new RichDescription("Import Jenkins Artifacts"),
            string.IsNullOrEmpty(projectName)
                ? new RichDescription("from the associated Jenkins build")
                : new RichDescription("from build ", new Hilite(buildNum), " in project ", new Hilite(projectName))
        );
    }
}
