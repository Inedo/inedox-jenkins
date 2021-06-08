using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.VariableTemplates;
using Inedo.Extensions.Jenkins.Credentials;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.Jenkins.ListVariableSources
{
    [DisplayName("Jenkins Branch Name")]
    [Description("Build names from a specified job in a Jenkins instance.")]
    public sealed class JenkinsBranchNameVariableSource : DynamicListVariableType, IHasCredentials<JenkinsLegacyCredentials>
    {
        [Persistent]
        [DisplayName("Credentials")]
        [TriggerPostBackOnChange]
        [Required]
        public string CredentialName { get; set; }

        [Persistent]
        [DisplayName("Job name")]
        [SuggestableValue(typeof(JobNameSuggestionProvider))]
        [Required]
        public string JobName { get; set; }

        public override async Task<IEnumerable<string>> EnumerateListValuesAsync(VariableTemplateContext context)
        {
            var credentials = (JenkinsLegacyCredentials)ResourceCredentials.TryCreate(JenkinsLegacyCredentials.TypeName, this.CredentialName, environmentId: null, applicationId: context.ProjectId, inheritFromParent: false);
            if (credentials == null)
                return Enumerable.Empty<string>();
            

            var client = new JenkinsClient(credentials.UserName, credentials.Password, credentials.ServerUrl, true, null, default);
            return await client.GetBranchNamesAsync(this.JobName).ConfigureAwait(false);
        }

        public override RichDescription GetDescription()
        {
            return new RichDescription("Jenkins (", new Hilite(this.CredentialName), ") ", " branches for ", new Hilite(this.JobName), ".");
        }
    }
}
