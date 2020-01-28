using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.Credentials;
using Inedo.Serialization;

namespace Inedo.Extensions.Jenkins.Credentials
{
    [DisplayName("Jenkins Project")]
    [Description("Connect to a Jenkins project to queue or import builds.")]
    public sealed class JenkinsSecureResource : SecureResource<UsernamePasswordCredentials, TokenCredentials>
    {
        [Required]
        [Persistent]
        [DisplayName("Jenkins server URL")]
        public string ServerUrl { get; set; }

        public override RichDescription GetDescription() => new RichDescription(this.ServerUrl);
    }
}
