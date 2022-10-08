using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Inedo.Extensions.Jenkins;

internal sealed class BranchNameSuggestionProvider : JenkinsSuggestionProvider
{
    protected override IAsyncEnumerable<string> GetSuggestionsAsync(JenkinsComponentConfiguration config, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(config.ProjectName) || !config.TryCreateClient(out var client))
            return AsyncEnumerable.Empty<string>();

        return client.GetBranchesAsync(config.ProjectName, cancellationToken);
    }
}
