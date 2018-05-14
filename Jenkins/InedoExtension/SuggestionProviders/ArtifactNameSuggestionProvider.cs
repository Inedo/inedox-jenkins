using System.Collections.Generic;
using System.Linq;
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
            var client = new JenkinsClient(credentials);
            var artifacts = await client.GetBuildArtifactsAsync(jobName, buildNumber).ConfigureAwait(false);
            return artifacts.Select(a => a.RelativePath);
        }
    }
}
