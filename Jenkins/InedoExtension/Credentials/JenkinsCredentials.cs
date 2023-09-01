using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Security;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.CIServers;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.Credentials;
using Inedo.Serialization;

namespace Inedo.Extensions.Jenkins;

[DisplayName("Jenkins Server")]
[Description("Connect to a Jenkins instance to import artifacts and trigger builds.")]
[PersistFrom("Inedo.Extensions.Jenkins.Credentials.JenkinsLegacyCredentials,Jenkins")]
public sealed class JenkinsCredentials : CIServiceCredentials<JenkinsService>, IMissingPersistentPropertyHandler
{
    [Required]
    [DisplayName("Jenkins server URL")]
    public override string? ServiceUrl { get; set; }

    [Persistent]
    [DisplayName("User name")]
    [PlaceholderText("Anonymous")]
    public override string? UserName { get; set; }
    
    [Persistent(Encrypted = true)]
    [DisplayName("API token / password")]
    [Description("For Jenkins version 1.426 and higher enter the API Token value as the password")]
    public override SecureString? Password { get; set; }

    [ScriptAlias("CsrfProtectionEnabled")]
    [DisplayName("Adds a CSRF token to the header of each request.")]
    [DefaultValue("$JenkinsCsrfProtectionEnabled")]
    public bool CsrfProtectionEnabled { get; set; }

    public override RichDescription GetCredentialDescription() => new(this.UserName);
    public override RichDescription GetServiceDescription() => new (this.ServiceUrl);
    
    internal static bool TryCreateFromCredentialName(string? credentialName, ICredentialResolutionContext? context, [NotNullWhen(true)] out JenkinsCredentials? credentials)
    {
        credentials =
            TryCreate(credentialName, context) switch
            {
                JenkinsCredentials j => j,
                TokenCredentials t => new() { Password = t.Token },
                UsernamePasswordCredentials u => new() { UserName = u.UserName, Password = u.Password },
                _ => new()
            };
        return credentials != null;
    }
    internal static bool TryCreateFromResourceName(string? resourceName, ICredentialResolutionContext? context, [NotNullWhen(true)] out JenkinsCredentials? credentials)
        => TryCreateFromCredentialName(SecureResource.TryCreate(SecureResourceType.CIProject, resourceName, context)?.CredentialName, context, out credentials);

    void IMissingPersistentPropertyHandler.OnDeserializedMissingProperties(IReadOnlyDictionary<string, string> missingProperties)
    {
        if (missingProperties.ContainsKey("ServerUrl"))
            this.ServiceUrl = missingProperties["ServerUrl"];
    }
}