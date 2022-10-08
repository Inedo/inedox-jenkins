using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using Inedo.Documentation;
using Inedo.Extensibility.CIServers;
using Inedo.Extensibility.Credentials;
using Inedo.Extensions.Credentials;
using Inedo.Serialization;

namespace Inedo.Extensions.Jenkins.Credentials;

[DisplayName("Jenkins Project")]
[Description("Connect to a Jenkins project or job to queue or import builds.")]
[PersistFrom("Inedo.Extensions.Jenkins.Credentials,Jenkins")]
public sealed class JenkinsProject : CIProject<JenkinsCredentials>, IMissingPersistentPropertyHandler
{
    [Persistent]
    [DisplayName("[Obsolete] Jenkins server URL")]
    [PlaceholderText("use the credential's URL")]
    [Description("In earlier versions, the Jenkins Server URL specified on the project. This should not be used going forward.")]
    public string? LegacyServerUrl { get; set; }

    private JenkinsClient CreateClient(ICredentialResolutionContext context)
    {
        if (string.IsNullOrEmpty(this.ProjectId))
            throw new InvalidOperationException($"{nameof(ProjectId)} is required.");

        JenkinsCredentials credentials =
            SecureCredentials.TryCreate(this.CredentialName, context) switch
            {
                JenkinsCredentials j => j,
                TokenCredentials t => new() { Password = t.Token },
                UsernamePasswordCredentials u => new() { UserName = u.UserName, Password = u.Password },
                _ => new ()
            };

        credentials.ServiceUrl ??= this.LegacyServerUrl 
            ?? throw new InvalidOperationException("No credentials or LegacyServerUrl."); 
        
        return new JenkinsClient(credentials);
    }

    public override IAsyncEnumerable<string> GetBuildArtifactsAsync(string buildId, ICredentialResolutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(buildId);
        JenkinsClient.ParseBuildId(buildId, out var branchName, out var buildNumber);
        return this.CreateClient(context).GetBuildArtifactsAsync(this.ProjectId!, branchName, buildNumber, cancellationToken);
    }

    public override IAsyncEnumerable<CIBuildInfo> GetBuildsAsync(ICredentialResolutionContext context, CancellationToken cancellationToken = default)
    {
        return this.CreateClient(context).GetBuildsAsync(this.ProjectId!, cancellationToken);
    }

    public override IAsyncEnumerable<string> GetScopesAsync(ICredentialResolutionContext context, CancellationToken cancellationToken = default)
    {
        return this.CreateClient(context).GetBranchesAsync(this.ProjectId!, cancellationToken);
    }

    public override IAsyncEnumerable<KeyValuePair<string, string>> GetBuildVariablesAsync(string buildId, ICredentialResolutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(buildId);
        JenkinsClient.ParseBuildId(buildId, out var branchName, out var buildNumber);
        return this.CreateClient(context).GetBuildVariablesAsync(this.ProjectId!, branchName, buildNumber, cancellationToken);
    }

    public override RichDescription GetDescription() => new (this.ProjectId);

    void IMissingPersistentPropertyHandler.OnDeserializedMissingProperties(IReadOnlyDictionary<string, string> missingProperties)
    {
        if (missingProperties.ContainsKey("ServerUrl"))
            this.LegacyServerUrl = missingProperties["ServerUrl"];
    }
}
