using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
    internal sealed class BuildNumberSuggestionProvider : ISuggestionProvider
    {
        private static readonly string[] CommonBuildNumbers = { "lastSuccessfulBuild", "lastStableBuild", "lastBuild", "lastCompletedBuild" };

#if BuildMaster
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var credentialName = config["CredentialName"];
            var jobName = config["JobName"];
            if (string.IsNullOrEmpty(credentialName) || string.IsNullOrEmpty(jobName))
                return Enumerable.Empty<string>();

            var credentials = ResourceCredentials.Create<JenkinsCredentials>(credentialName);
            var client = new JenkinsClient(credentials);            

            var buildNumbers = await client.GetBuildNumbersAsync(jobName).ConfigureAwait(false);
            return CommonBuildNumbers.Concat(buildNumbers);
        }
#elif Otter
        public IEnumerable<string> GetSuggestions(object context)
        {
            throw new NotImplementedException();
        }
#endif
    }
}
