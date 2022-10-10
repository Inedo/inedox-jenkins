using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensions.Jenkins.Credentials;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.Jenkins.ListVariableSources;

[DisplayName("Jenkins Build Number")]
[Description("Build numbers from a specified project in a Jenkins server.")]
public sealed class JenkinsBuildNumberVariableSource : JenkinsVariableSourceBase
{
    [Persistent]
    [DisplayName("Jenkins resource")]
    [TriggerPostBackOnChange]
    [Required]
    [SuggestableValue(typeof(SecureResourceSuggestionProvider<JenkinsProject>))]
    public override string? ResourceName { get; set; }

    [Persistent]
    [DisplayName("Branch name")]
    [SuggestableValue(typeof(BranchNameSuggestionProvider))]
    [PlaceholderText("required for multi-branch projects")]
    public string? BranchName { get; set; }

    internal override IEnumerable<string> EnumerateDefault() => JenkinsClient.SpecialBuildNumbers.AsEnumerable();

    internal override async Task<IEnumerable<string>> EnumerateListValuesAsync(JenkinsClient client, string projectName)
    {
        var list = new List<string>(JenkinsClient.SpecialBuildNumbers);

        await foreach (var b in client.GetBuildsAsync(projectName, this.BranchName).ConfigureAwait(false))
            list.Add(b.Id);

        return list;
    }
    public override RichDescription GetDescription()
    {
        return new RichDescription(
            "Jenkins (", new Hilite(this.ResourceName ?? this.LegacyCredentialName), ") ", 
            " build numbers",
            string.IsNullOrEmpty(this.BranchName) ? "" : $" on branch {this.BranchName}",
            ".");
    }
}
