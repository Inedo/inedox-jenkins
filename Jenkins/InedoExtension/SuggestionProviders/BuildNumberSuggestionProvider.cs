using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Inedo.Extensions.Jenkins;

internal sealed class BuildNumberSuggestionProvider : JenkinsSuggestionProvider
{
    protected override async IAsyncEnumerable<string> GetSuggestionsAsync(JenkinsComponentConfiguration config, [EnumeratorCancellation]CancellationToken cancellationToken)
    {
        foreach (var i in JenkinsClient.SpecialBuildNumbers)
            yield return i;

        if (string.IsNullOrEmpty(config.ProjectName) || !config.TryCreateClient(out var client))
            yield break;

        List<string> builds;
        try
        {
            builds = await client.GetBuildsAsync(config.ProjectName, config.BranchName, cancellationToken)
                .Select(b => b.Number)
                .ToListAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            yield break;
        }
        foreach (var i in builds)
            yield return i;

    }
}
