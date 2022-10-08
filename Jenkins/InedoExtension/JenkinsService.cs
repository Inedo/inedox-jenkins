using System.Collections.Generic;
using System.Threading;
using Inedo.Extensibility.CIServers;
using Inedo.Extensions.Jenkins.Credentials;
using Inedo.Extensions.Jenkins.Operations;

#nullable enable

namespace Inedo.Extensions.Jenkins;

public sealed class JenkinsService : CIService<JenkinsProject, JenkinsCredentials, ImportJenkinsArtifactsOperation>
{
    public override string ServiceName => "Jenkins";
    public override string VariablesDisplayName => "Parameters";
    public override string? ScopeDisplayName => "Branch";
    public override string ApiUrlDisplayName => "Jenkins Server URL";
    public override string PasswordDisplayName => "Password or API Token";

    public override IAsyncEnumerable<CIProjectInfo> GetProjectsAsync(JenkinsCredentials credentials, CancellationToken cancellationToken = default)
    {
        var client = new JenkinsClient(credentials);
        return client.GetProjectsAsync(cancellationToken);
    }
}