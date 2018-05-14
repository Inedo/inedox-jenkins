using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensions.Jenkins.Credentials;
using Inedo.Web;

namespace Inedo.Extensions.Jenkins
{
    internal sealed class BuildNumberSuggestionProvider : ISuggestionProvider
    {
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var credentialName = config["CredentialName"];
            var jobName = config["JobName"];
            if (string.IsNullOrEmpty(credentialName) || string.IsNullOrEmpty(jobName))
                return Enumerable.Empty<string>();

            var credentials = ResourceCredentials.Create<JenkinsCredentials>(credentialName);
            var client = new JenkinsClient(credentials);            

            var buildNumbers = await client.GetBuildNumbersAsync(jobName).ConfigureAwait(false);
            return buildNumbers;
        }
    }
}
