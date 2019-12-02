using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
            var branchName = config["BranchName"];
            if (string.IsNullOrEmpty(credentialName) || string.IsNullOrEmpty(jobName))
                return Enumerable.Empty<string>();

            int? projectId = AH.ParseInt(AH.CoalesceString(config["ProjectId"], config["ApplicationId"]));
            int? environmentId = AH.ParseInt(config["EnvironmentId"]);

            var credentials = (JenkinsCredentials)ResourceCredentials.TryCreate(JenkinsCredentials.TypeName, credentialName, environmentId: environmentId, applicationId: projectId, inheritFromParent: false);
            if (credentials == null)
                return Enumerable.Empty<string>();

            using (var cts = new CancellationTokenSource(new TimeSpan(0, 0, 30)))
            {
                var client = new JenkinsClient(credentials, null, cts.Token);
                return await client.GetBuildNumbersAsync(jobName, branchName).ConfigureAwait(false);
            }
        }
    }
}
