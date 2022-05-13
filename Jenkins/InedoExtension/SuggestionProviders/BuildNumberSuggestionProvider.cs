using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Extensibility;
using Inedo.Web;

namespace Inedo.Extensions.Jenkins
{
    internal sealed class BuildNumberSuggestionProvider : ISuggestionProvider
    {
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var jobName = config["JobName"];
            if (string.IsNullOrEmpty(jobName))
                return Enumerable.Empty<string>();

            var branchName = config["BranchName"];

            using (var cts = new CancellationTokenSource(new TimeSpan(0, 0, 30)))
            {
                var client = this.CreateClient(config, cts.Token);
                if (client == null)
                    return Enumerable.Empty<string>();
                return await client.GetBuildNumbersAsync(jobName, branchName).ConfigureAwait(false);
            }
        }
    }
}
