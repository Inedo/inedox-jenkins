using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensibility.VariableTemplates;
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

    internal async override Task<IEnumerable<string>> EnumerateListValuesAsync(JenkinsClient client, string projectName)
    {
        return await client.GetBranchesAsync(projectName).ToListAsync().ConfigureAwait(false);
    }

    public override RichDescription GetDescription()
    {
        return new RichDescription("Jenkins (", new Hilite(this.ResourceName ?? this.LegacyCredentialName), ") branch names.");
    }
}
