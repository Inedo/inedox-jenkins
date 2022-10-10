using System.Runtime.CompilerServices;

namespace Inedo.Extensions.Jenkins;

internal sealed class ProjectNameSuggestionProvider : JenkinsSuggestionProvider
{
    protected override async IAsyncEnumerable<string> GetSuggestionsAsync(JenkinsComponentConfiguration config, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!config.TryCreateClient(out var client))
            yield break;

        await foreach (var p in client.GetProjectsAsync(cancellationToken).ConfigureAwait(false))
            yield return p.Id;
    }
}
