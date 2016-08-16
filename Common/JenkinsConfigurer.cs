using System;
#if BuildMaster
using Inedo.BuildMaster.Extensibility.Configurers.Extension;
using Inedo.BuildMaster.Web;
#elif Otter
using Inedo.Otter.Extensibility.Configurers.Extension;
using Inedo.Otter.Web;
#endif
using Inedo.Serialization;

[assembly: ExtensionConfigurer(typeof(Inedo.Extensions.Jenkins.JenkinsConfigurer))]

namespace Inedo.Extensions.Jenkins
{
    [Serializable]
    [CustomEditor(typeof(JenkinsConfigurerEditor))]
    public sealed class JenkinsConfigurer : ExtensionConfigurerBase, IJenkinsConnectionInfo
    {
        string IJenkinsConnectionInfo.UserName => this.Username; // Rubbish casing

        [Persistent]
        public string ServerUrl { get; set; }

        [Persistent]
        public string Username { get; set; }

        [Persistent(Encrypted = true)]
        public string Password { get; set; }
    }
}
