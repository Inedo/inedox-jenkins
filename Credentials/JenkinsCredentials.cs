using System.ComponentModel;
using System.Security;
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Credentials;
using Inedo.BuildMaster.Web;
using Inedo.Documentation;
using Inedo.Serialization;

namespace Inedo.BuildMasterExtensions.Jenkins.Credentials
{
    [ScriptAlias("Jenkins")]
    [DisplayName("Jenkins")]
    [Description("Credentials for Jenkins.")]
    public sealed class JenkinsCredentials : ResourceCredentials
    {
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
    }
}
