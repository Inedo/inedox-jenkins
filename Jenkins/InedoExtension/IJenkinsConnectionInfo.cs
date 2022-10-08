using System.Diagnostics.CodeAnalysis;
using System.Security;
using Inedo.Diagnostics;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.Jenkins.Credentials;

namespace Inedo.Extensions.Jenkins;

interface IJenkinsCredentialsConfig
{
    string? ResourceName { get; }
    string? CredentialName { get; }
    string? ServerUrl { get; }
    string? UserName { get; }
    SecureString? Password { get; }
    bool? CsrfProtectionEnabled { get; }
}
interface IJenkinsProjectConfig : IJenkinsCredentialsConfig
{
    string? BranchName { get; }
    string? ProjectName { get; }
}

internal static class IJenkinsCredentialsConfigExtensions
{
    public static bool TryCreateClient(this IJenkinsCredentialsConfig config, ICredentialResolutionContext context, [NotNullWhen(true)] out JenkinsClient? client)
    {
        var creds = config.GetCredentials(context);
        client = string.IsNullOrEmpty(creds.ServiceUrl) ? null : new JenkinsClient(creds, config as ILogSink);
        return client != null;
    }
    public static JenkinsCredentials GetCredentials(this IJenkinsCredentialsConfig config, ICredentialResolutionContext context)
    {
        var project = SecureResource.TryCreate(config.ResourceName, context) as JenkinsProject;

        var credentialName = config.CredentialName ?? project?.CredentialName;

        if (!JenkinsCredentials.TryCreateFromCredentialName(credentialName, context, out var credentials))
            credentials = new();

        credentials.UserName = config.UserName ?? credentials.UserName;
        credentials.Password = config.Password ?? credentials.Password;
        credentials.ServiceUrl = config.ServerUrl ?? credentials.ServiceUrl ?? project?.LegacyServerUrl;
        credentials.CsrfProtectionEnabled = config.CsrfProtectionEnabled ?? credentials.CsrfProtectionEnabled;
        return credentials;
    }
}

