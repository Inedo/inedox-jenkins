using System.ComponentModel;
using System.Security;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.Credentials;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.Jenkins.Credentials
{
    [ScriptAlias(JenkinsCredentials.TypeName)]
    [DisplayName("Jenkins")]
    [Description("Credentials for Jenkins.")]
    public sealed class JenkinsCredentials : ResourceCredentials
    {
        public const string TypeName = "Jenkins";

        [Required]
        [Persistent]
        [DisplayName("Jenkins server URL")]
        public string ServerUrl { get; set; }

        [Persistent]
        [DisplayName("User name")]
        [PlaceholderText("Anonymous")]
        public string UserName { get; set; }

        [Persistent(Encrypted = true)]
        [DisplayName("API token / password")]
        [Description("For Jenkins version 1.426 and higher enter the API Token value as the password")]
        [FieldEditMode(FieldEditMode.Password)]
        public SecureString Password { get; set; }

        public override RichDescription GetDescription()
        {
            return new RichDescription(string.IsNullOrEmpty(this.UserName) ? "Guest" : this.UserName);
        }

        public string BaseUrl => (this.ServerUrl ?? "").TrimEnd('/');

        public override SecureCredentials ToSecureCredentials()
        {
            if (string.IsNullOrEmpty(this.UserName))
                return new TokenCredentials { Token = this.Password };
            else
                return new Inedo.Extensions.Credentials.UsernamePasswordCredentials { UserName = this.UserName, Password = this.Password };
        }

        public override SecureResource ToSecureResource() => new JenkinsSecureResource { ServerUrl = this.ServerUrl };
    }
}
