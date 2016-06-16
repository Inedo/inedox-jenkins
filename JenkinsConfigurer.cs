using System;
using Inedo.BuildMaster.Extensibility.Configurers.Extension;
using Inedo.BuildMaster.Web;
using Inedo.Serialization;

[assembly: ExtensionConfigurer(typeof(Inedo.BuildMasterExtensions.Jenkins.JenkinsConfigurer))]

namespace Inedo.BuildMasterExtensions.Jenkins
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
