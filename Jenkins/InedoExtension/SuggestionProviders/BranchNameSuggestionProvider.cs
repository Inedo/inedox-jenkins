namespace Inedo.Extensions.Jenkins;

internal sealed class BranchNameSuggestionProvider : JenkinsSuggestionProvider
{
    protected override IAsyncEnumerable<string> GetSuggestionsAsync(JenkinsComponentConfiguration config, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(config.ProjectName) || !config.TryCreateClient(out var client))
            return Enumerable.Empty<string>().ToAsyncEnumerable();

        return client.GetBranchesAsync(config.ProjectName, cancellationToken);
    }
}
