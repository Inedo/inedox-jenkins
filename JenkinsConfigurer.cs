using System;
using Inedo.BuildMaster;
using Inedo.BuildMaster.Extensibility.Configurers.Extension;
using Inedo.BuildMaster.Web;

[assembly: ExtensionConfigurer(typeof(Inedo.BuildMasterExtensions.Jenkins.JenkinsConfigurer))]

namespace Inedo.BuildMasterExtensions.Jenkins
{
    [CustomEditor(typeof(JenkinsConfigurerEditor))]
    [Serializable]
    public sealed class JenkinsConfigurer : ExtensionConfigurerBase
    {
        [Persistent]
        public string ServerUrl { get; set; }

        [Persistent]
        public string Username { get; set; }

        [Persistent(Encrypted=true)]
        public string Password { get; set; }

        public string BaseUrl
        {
            get
            {
                return (this.ServerUrl ?? "").TrimEnd('/');
            }
        }

        public override string ToString()
        {
            return string.Empty;
        }
    }
}
