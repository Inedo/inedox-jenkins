using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Jenkins.Credentials;
using Inedo.IO;
using Inedo.Web;

namespace Inedo.Extensions.Jenkins.Operations;

[Description("Downloads artifact files from a Jenkins build to the specified directory. Note this will not save them in BuildMaster's artifacts.")]
[ScriptNamespace("Jenkins")]
[ScriptAlias("Download-Artifacts"), ScriptAlias("Download-Artifact", Obsolete = true)]
public sealed class DownloadJenkinsArtifactOperation : JenkinsOperation
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
    [ScriptAlias("To"), ScriptAlias("TargetDirectory")]
    [DisplayName("Target directory")]
    [DefaultValue("$WorkingDirectory"), NotNull]
    [FieldEditMode(FieldEditMode.ServerDirectoryPath)]
    public string? TargetDirectory { get; set; }


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

    [Undisclosed]
    [ScriptAlias("Artifact", Obsolete = true)]
    public string? ArtifactName { get; set; }
    [Undisclosed]
    [ScriptAlias("ExtractFiles", Obsolete = true)]
    [DefaultValue(true)]
    public bool ExtractFilesToTargetDirectory { get; set; } = true;

    public override async Task ExecuteAsync(IOperationExecutionContext context)
    {
        if (!string.IsNullOrEmpty(this.ArtifactName))
        {
            this.LogWarning("The ArtifactName parameter is no longer supported; use Includes instead.");
            if (this.ArtifactName == "*")
            {
                this.LogDebug("Using \"**\" to Includes instead of \"*\" to retrieve all files");
                this.Includes = new[] { "**" };
            }
            else
                this.Includes = new[] { this.ArtifactName };
        }
        if (!this.ExtractFilesToTargetDirectory)
            throw new ExecutionFailureException($"Setting ExtractFilesToTargetDirectory to false is no longer supported.");
        if (this.ProjectName == null)
            throw new ExecutionFailureException($"No Jenkins project was specified, and there is no CI build associated with this execution.");
        if (this.BuildNumber == null)
            throw new ExecutionFailureException($"No Jenkins build was specified, and there is no CI build associated with this execution.");
        if (!this.TryCreateClient(context, out var client))
            throw new ExecutionFailureException($"Could not create a connection to Jenkins resource \"{AH.CoalesceString(this.ResourceName, this.ServerUrl)}\".");

        int actualBuildNumber = await client.GetActualBuildNumber(this.ProjectName, this.BranchName, this.BuildNumber, context.CancellationToken);
        this.JenkinsBuildNumber = actualBuildNumber.ToString();

        var targetDirectory = context.ResolvePath(this.TargetDirectory);
        var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false);

        var count = 0;
        var mask = new MaskingContext(this.Includes, this.Excludes);
        await foreach (var artifact in client.GetBuildArtifactsAsync(this.ProjectName, this.BranchName, actualBuildNumber, context.CancellationToken))
        {
            if (!mask.IsMatch(artifact))
                continue;
            
            count++;

            var fileName = PathEx.Combine(targetDirectory, artifact);
            this.LogDebug("Target local file: " + fileName);
            await fileOps.CreateDirectoryAsync(PathEx.GetDirectoryName(fileName)!).ConfigureAwait(false);

            using var fileStream = await fileOps.OpenFileAsync(fileName, FileMode.Create, FileAccess.Write).ConfigureAwait(false);
            await client.DownloadArtifactAsync(this.ProjectName, this.BranchName, actualBuildNumber, artifact, fileStream, context.CancellationToken);

        }
        if (count == 0)
            this.LogWarning("No artifacts were downloaded from Jenkins.");
        else
            this.LogInformation($"{count} artifacts were downloaded from Jenkins.");
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
            new RichDescription("Download Jenkins Artifacts"),
            string.IsNullOrEmpty(projectName)
                ? new RichDescription("from the associated Jenkins build to ", config[nameof(this.TargetDirectory)])
                : new RichDescription("from build ", new Hilite(buildNum), " in project ", new Hilite(projectName), " to ", config[nameof(this.TargetDirectory)])
        );

    }
}
