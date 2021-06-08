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
using static Inedo.Extensions.Jenkins.InlineIf;

namespace Inedo.Extensions.Jenkins.ListVariableSources
{
    [DisplayName("Jenkins Build Number")]
    [Description("Build numbers from a specified job in a Jenkins instance.")]
    public sealed class JenkinsBuildNumberVariableSource : DynamicListVariableType, IHasCredentials<JenkinsLegacyCredentials>
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

        [Persistent]
        [DisplayName("Branch name")]
        [SuggestableValue(typeof(BranchNameSuggestionProvider))]
        public string BranchName { get; set; }

        public override async Task<IEnumerable<string>> EnumerateListValuesAsync(VariableTemplateContext context)
        {
            var credentials = (JenkinsLegacyCredentials)ResourceCredentials.TryCreate(JenkinsLegacyCredentials.TypeName, this.CredentialName, environmentId: null, applicationId: context.ProjectId, inheritFromParent: false);
            if (credentials == null)
                return Enumerable.Empty<string>();

            var client = new JenkinsClient(credentials.UserName, credentials.Password, credentials.ServerUrl, true, null, default);
            return await client.GetBuildNumbersAsync(this.JobName, this.BranchName).ConfigureAwait(false);
        }

        public override RichDescription GetDescription()
        {
            return new RichDescription("Jenkins (", new Hilite(this.CredentialName), ") ", " builds for ", new Hilite(this.JobName), IfHasValue(this.BranchName, " on branch ", new Hilite(this.BranchName)), ".");
        }
    }
}
