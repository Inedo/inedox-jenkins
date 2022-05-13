using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensibility.VariableTemplates;
using Inedo.Extensions.Jenkins.Credentials;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.Jenkins.ListVariableSources
{
    [DisplayName("Jenkins Branch Name")]
    [Description("Build names from a specified job in a Jenkins instance.")]
    public sealed class JenkinsBranchNameVariableSource : DynamicListVariableType, IMissingPersistentPropertyHandler
    {
        [Persistent]
        [DisplayName("Jenkins resource")]
        [TriggerPostBackOnChange]
        [Required]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<JenkinsSecureResource>))] 
        public string ResourceName { get; set; }

        [Persistent]
        [DisplayName("Job name")]
        [SuggestableValue(typeof(JobNameSuggestionProvider))]
        [Required]
        public string JobName { get; set; }

        public override async Task<IEnumerable<string>> EnumerateListValuesAsync(VariableTemplateContext context)
        {
            var credContext = new CredentialResolutionContext(context.ProjectId, null);
            var client = new JenkinsClient(this.ResourceName, credContext, true, null, default);
            return await client.GetBranchNamesAsync(this.JobName).ConfigureAwait(false);
        }

        public override RichDescription GetDescription()
        {
            return new RichDescription("Jenkins (", new Hilite(this.ResourceName), ") ", " branches for ", new Hilite(this.JobName), ".");
        }

        void IMissingPersistentPropertyHandler.OnDeserializedMissingProperties(IReadOnlyDictionary<string, string> missingProperties)
        {
            if (missingProperties.ContainsKey("CredentialName"))
                this.ResourceName = missingProperties["CredentialName"];
        }
    }
}
