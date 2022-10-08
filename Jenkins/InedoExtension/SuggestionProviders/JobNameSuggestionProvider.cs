using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Inedo.Extensions.Jenkins;

internal sealed class ProjectNameSuggestionProvider : JenkinsSuggestionProvider
{
    protected override IAsyncEnumerable<string> GetSuggestionsAsync(JenkinsComponentConfiguration config, CancellationToken cancellationToken)
    {
        if (!config.TryCreateClient(out var client))
            return AsyncEnumerable.Empty<string>();

        return client.GetProjectsAsync(cancellationToken).Select(c => c.Id);
    }
}
