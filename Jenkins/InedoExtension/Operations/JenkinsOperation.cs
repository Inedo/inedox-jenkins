using System.ComponentModel;
using System.Security;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Jenkins.Credentials;
using Inedo.Web;

#nullable enable

namespace Inedo.Extensions.Jenkins.Operations
{
    public abstract class JenkinsOperation : ExecuteOperation, IJenkinsCredentialsConfig, IJenkinsProjectConfig
    {
        public abstract string? ResourceName { get; set; }
        public abstract string? ProjectName { get; set; }
        public abstract string? BranchName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Credentials")]
        [DisplayName("Jenkins service name")]
        [PlaceholderText("Use name from Jenkins resource")]
        [SuggestableValue(typeof(SecureCredentialsSuggestionProvider<JenkinsCredentials>))]
        public string? CredentialName { get; set; }
        [Category("Connection/Identity")]
        [ScriptAlias("Server")]
        [DisplayName("Server URL")]
        [PlaceholderText("Use service's server URL")]
        public string? ServerUrl { get; set; }
        [Category("Connection/Identity")]
        [ScriptAlias("UserName")]
        [DisplayName("User name")]
        [PlaceholderText("Use credential's Username")]
        public string? UserName { get; set; }
        [Category("Connection/Identity")]
        [ScriptAlias("Password")]
        [DisplayName("API token or password")]
        [PlaceholderText("Use credential's token/password")]
        public SecureString? Password { get; set; }
        [Category("Connection/Identity")]
        [ScriptAlias("CsrfProtectionEnabled")]
        [DisplayName("Use CSRF token")]
        [DefaultValue(true)]
        [PlaceholderText("Use credential's CRSF setting")]
        public bool? CsrfProtectionEnabled { get; set; } = true;

    }
}
