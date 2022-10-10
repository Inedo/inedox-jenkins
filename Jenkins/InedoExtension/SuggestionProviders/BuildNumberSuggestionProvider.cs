using System.Runtime.CompilerServices;

namespace Inedo.Extensions.Jenkins;

internal sealed class BuildNumberSuggestionProvider : JenkinsSuggestionProvider
{
    protected override async IAsyncEnumerable<string> GetSuggestionsAsync(JenkinsComponentConfiguration config, [EnumeratorCancellation]CancellationToken cancellationToken)
    {
        foreach (var i in JenkinsClient.SpecialBuildNumbers)
            yield return i;

        if (string.IsNullOrEmpty(config.ProjectName) || !config.TryCreateClient(out var client))
            yield break;

        await foreach (var b in client.GetBuildsAsync(config.ProjectName, config.BranchName, cancellationToken).ConfigureAwait(false))
            yield return b.Number;
    }
}
