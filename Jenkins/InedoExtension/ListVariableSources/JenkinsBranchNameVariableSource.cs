using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensions.Jenkins.Credentials;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.Jenkins.ListVariableSources;

[DisplayName("Jenkins Branch Name")]
[Description("Build names from a specified multi-branch project in a Jenkins instance.")]
public sealed class JenkinsBranchNameVariableSource : JenkinsVariableSourceBase
{
    [Persistent]
    [DisplayName("Jenkins resource")]
    [TriggerPostBackOnChange]
    [Required]
    [SuggestableValue(typeof(SecureResourceSuggestionProvider<JenkinsProject>))]
    public override string? ResourceName { get; set; }

    internal override IAsyncEnumerable<string> EnumerateListValuesAsync(JenkinsClient client, string projectName) => client.GetBranchesAsync(projectName);

    public override RichDescription GetDescription()
    {
        return new RichDescription("Jenkins (", new Hilite(this.ResourceName ?? this.LegacyCredentialName), ") branch names.");
    }
}
