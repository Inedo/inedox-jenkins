using System.ComponentModel;
using System.Security;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Jenkins.Credentials;
using Inedo.Web;

namespace Inedo.Extensions.Jenkins.Operations
{
    public abstract class JenkinsOperation : ExecuteOperation, IJenkinsConnectionInfo, IJenkinsConfig
    {
        [ScriptAlias("From")]
        [DisplayName("Jenkins server URL")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<JenkinsSecureResource>))]
        public string ResourceName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Server")]
        [DisplayName("Jenkins server URL")]
        public string ServerUrl { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Credentials")]
        [DisplayName("Credentials")]
        [PlaceholderText("Use resource")]
#warning suggestion provider for credentials
        //[SuggestableValue(typeof(SecureCredentialsSuggestionProvider<Inedo.su>))]
        public string CredentialName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("UserName")]
        [DisplayName("User name")]
        [PlaceholderText("Anonymous")]
        public string UserName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Password")]
        [DisplayName("API token / password")]
        [Description("For Jenkins version 1.426 and higher enter the API Token value as the password")]
        public SecureString Password { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("CsrfProtectionEnabled")]
        [DisplayName("Adds a CSRF token to the header of each request.")]
        [DefaultValue("$JenkinsCsrfProtectionEnabled")]
        public bool CsrfProtectionEnabled { get; set; }
    }
}
