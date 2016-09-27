using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Inedo.Extensions.Jenkins.Operations;
using Inedo.Extensions.Jenkins.Credentials;

#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Credentials;
using Inedo.BuildMaster.Web.Controls;
#elif Otter
using Inedo.Otter.Web.Controls;
#endif

namespace Inedo.Extensions.Jenkins
{
    internal sealed class JobNameSuggestionProvider : ISuggestionProvider
    {
#if BuildMaster
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var credentialName = config[nameof(JenkinsOperation.CredentialName)];
            if (string.IsNullOrEmpty(credentialName))
                return Enumerable.Empty<string>();

            var credentials = ResourceCredentials.Create<JenkinsCredentials>(credentialName);
            var client = new JenkinsClient(credentials);
            var jobs = await client.GetJobNamesAsync().ConfigureAwait(false);
            return jobs;
        }
#elif Otter
        public IEnumerable<string> GetSuggestions(object context)
        {
            throw new NotImplementedException();
        }
#endif
    }
}
