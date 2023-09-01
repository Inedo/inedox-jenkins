using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensibility.VariableTemplates;
using Inedo.Extensions.Jenkins.Credentials;
using Inedo.Serialization;
using Inedo.Web;
using Microsoft.VisualBasic;

namespace Inedo.Extensions.Jenkins.ListVariableSources;

public abstract class JenkinsVariableSourceBase : DynamicListVariableType, IMissingPersistentPropertyHandler
{
    [Persistent]
    [Category("Legacy")]
    [DisplayName("Jenkins credential name")]
    [SuggestableValue(typeof(SecureCredentialsSuggestionProvider<JenkinsCredentials>))]
    public string? LegacyCredentialName { get; set; }

    [Persistent]
    [Category("Legacy")]
    [DisplayName("Job name")]
    public string? LegacyJobName { get; set; }

    public abstract string? ResourceName { get; set; }

    public sealed async override Task<IEnumerable<string>> EnumerateListValuesAsync(VariableTemplateContext context)
    {
        JenkinsCredentials? credentials; string? projectName;
        if (!string.IsNullOrEmpty(this.LegacyCredentialName) && !string.IsNullOrEmpty(this.LegacyJobName))
        {
            if (!JenkinsCredentials.TryCreateFromCredentialName(LegacyCredentialName, context, out credentials))
                return this.EnumerateDefault();

            projectName = this.LegacyJobName;
        }
        else if (!string.IsNullOrEmpty(this.ResourceName))
        {
            if (SecureResource.TryCreate(SecureResourceType.CIProject, this.ResourceName, context) is not JenkinsProject projectRENAME
                || projectRENAME.ProjectId == null
                || !JenkinsCredentials.TryCreateFromCredentialName(projectRENAME.CredentialName, context, out credentials))
                return this.EnumerateDefault();
            
            projectName = projectRENAME.ProjectId;
        }
        else
            return this.EnumerateDefault();

        try
        {
            return await this.EnumerateListValuesAsync(new JenkinsClient(credentials), projectName);
        }
        catch
        {
            return this.EnumerateDefault();
        }
    }
    internal abstract Task<IEnumerable<string>> EnumerateListValuesAsync(JenkinsClient client, string projectName);
    internal virtual IEnumerable<string> EnumerateDefault() => Enumerable.Empty<string>();

    void IMissingPersistentPropertyHandler.OnDeserializedMissingProperties(IReadOnlyDictionary<string, string> missingProperties)
    {
        if (missingProperties.ContainsKey("CredentialName"))
            this.LegacyCredentialName = missingProperties["CredentialName"];

        if (missingProperties.ContainsKey("JobName"))
            this.LegacyJobName = missingProperties["JobName"];
    }
}
