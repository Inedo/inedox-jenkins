using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Web;

namespace Inedo.Extensions.Jenkins;

internal abstract class JenkinsSuggestionProvider : ISuggestionProvider
{
    async Task<IEnumerable<string>> ISuggestionProvider.GetSuggestionsAsync(IComponentConfiguration config)
    {
        var list = new List<string>();
        using var cts = new CancellationTokenSource(new TimeSpan(0, 0, 30));
        await foreach (var item in this.GetSuggestionsAsync(new JenkinsComponentConfiguration(config), cts.Token))
            list.Add(item);
        return list.AsEnumerable();
    }
    IAsyncEnumerable<string> ISuggestionProvider.GetSuggestionsAsync(string startsWith, IComponentConfiguration config, CancellationToken cancellationToken)
        => this.GetSuggestionsAsync(new JenkinsComponentConfiguration(config), cancellationToken);

    protected abstract IAsyncEnumerable<string> GetSuggestionsAsync(JenkinsComponentConfiguration config, CancellationToken cancellationToken);

    protected sealed class JenkinsComponentConfiguration : IJenkinsCredentialsConfig, IComponentConfiguration, IJenkinsProjectConfig
    {
        private readonly IComponentConfiguration config;
        public JenkinsComponentConfiguration(IComponentConfiguration config) => this.config = config;
        
        private string? GetString([CallerMemberName] string? key = null) 
            => AH.NullIf(config[key], string.Empty);
        private bool? GetBool([CallerMemberName] string? key = null) 
            => string.IsNullOrEmpty(config[key]) ? null : bool.TrueString.Equals(config[key], System.StringComparison.OrdinalIgnoreCase);
        private SecureString? GetSecString([CallerMemberName] string? key = null) 
            => string.IsNullOrEmpty(config[key]) ? null : AH.CreateSecureString(config[key]);

        public string? ResourceName => this.GetString();
        public string? CredentialName => this.GetString();
        public string? ServerUrl => this.GetString();
        public string? UserName => this.GetString();
        public SecureString? Password => this.GetSecString();
        public bool? CsrfProtectionEnabled => this.GetBool();

        public string? BranchName => this.GetString();
        public string? ProjectName => this.GetString();

        public object EditorContext => this.config.EditorContext;


        public ComponentConfigurationValue this[string propertyName] => this.config[propertyName];

        public bool TryCreateClient([NotNullWhen(true)] out JenkinsClient? client)
        {
            var creds = this.GetCredentials(this.config.EditorContext as ICredentialResolutionContext ?? CredentialResolutionContext.None);
            client = string.IsNullOrEmpty(creds.ServiceUrl) ? null : new JenkinsClient(creds);
            return client != null;
        }
    }
}
