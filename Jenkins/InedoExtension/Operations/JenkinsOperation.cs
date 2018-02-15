using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Jenkins.Credentials;

namespace Inedo.Extensions.Jenkins.Operations
{
    public abstract class JenkinsOperation : ExecuteOperation, IHasCredentials<JenkinsCredentials>, IJenkinsConnectionInfo
    {
        public abstract string CredentialName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Server")]
        [DisplayName("Jenkins server URL")]
        [MappedCredential(nameof(JenkinsCredentials.ServerUrl))]
        public string ServerUrl { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("UserName")]
        [DisplayName("User name")]
        [PlaceholderText("Anonymous")]
        [MappedCredential(nameof(JenkinsCredentials.UserName))]
        public string UserName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Password")]
        [DisplayName("API token / password")]
        [Description("For Jenkins version 1.426 and higher enter the API Token value as the password")]
        [MappedCredential(nameof(JenkinsCredentials.Password))]
        public string Password { get; set; }
    }
}
