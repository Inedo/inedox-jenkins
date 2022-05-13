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
    internal sealed class JobNameSuggestionProvider : ISuggestionProvider
    {
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            using (var cts = new CancellationTokenSource(new TimeSpan(0, 0, 30)))
            {
                var client = this.CreateClient(config, cts.Token);
                if (client == null)
                    return Enumerable.Empty<string>();
                return await client.GetJobNamesAsync().ConfigureAwait(false);
            }
        }
    }
}
