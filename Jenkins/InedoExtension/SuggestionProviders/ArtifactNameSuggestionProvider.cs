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
    internal sealed class ArtifactNameSuggestionProvider : ISuggestionProvider
    {
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var credentialName = config["CredentialName"];
            var jobName = config["JobName"];
            if (string.IsNullOrEmpty(credentialName) || string.IsNullOrEmpty(jobName))
                return Enumerable.Empty<string>();

            string buildNumber = AH.CoalesceString(config["BuildNumber"], "lastSuccessfulBuild");

            var credentials = ResourceCredentials.Create<JenkinsCredentials>(credentialName);
            using (var cts = new CancellationTokenSource(new TimeSpan(0, 0, 30)))
            {
                var client = new JenkinsClient(credentials, null, cts.Token);
                return (await client.GetBuildArtifactsAsync(jobName, buildNumber))
                    .Select(a => a.RelativePath)
                    .ToList();
            }
        }
    }
}
