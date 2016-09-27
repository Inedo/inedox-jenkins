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
    internal sealed class ArtifactNameSuggestionProvider : ISuggestionProvider
    {
#if BuildMaster
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var credentialName = config["CredentialName"];
            var jobName = config["JobName"];
            if (string.IsNullOrEmpty(credentialName) || string.IsNullOrEmpty(jobName))
                return Enumerable.Empty<string>();


            var credentials = ResourceCredentials.Create<JenkinsCredentials>(credentialName);
            var client = new JenkinsClient(credentials);
            var artifacts = await client.GetBuildArtifactsAsync(jobName, "lastSuccessfulBuild");
            return artifacts.Select(a => a.RelativePath);
        }
#elif Otter
        public IEnumerable<string> GetSuggestions(object context)
        {
            throw new NotImplementedException();
        }
#endif
    }
}
