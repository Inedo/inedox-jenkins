using System.Linq;
using Inedo.BuildMaster;
using Inedo.BuildMaster.Data;
using Inedo.BuildMaster.Extensibility.Configurers.Extension;
using Inedo.BuildMaster.Web;

[assembly: ExtensionConfigurer(typeof(Inedo.BuildMasterExtensions.Jenkins.JenkinsConfigurer))]

namespace Inedo.BuildMasterExtensions.Jenkins
{
    [CustomEditor(typeof(JenkinsConfigurerEditor))]
    public sealed class JenkinsConfigurer : ExtensionConfigurerBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JenkinsConfigurer"/> class.
        /// </summary>
        public JenkinsConfigurer()
        {
        }

        public static readonly string ConfigurerName = typeof(JenkinsConfigurer).FullName + "," + typeof(JenkinsConfigurer).Assembly.GetName().Name;

        /// <summary>
        /// Gets or sets the server URL without the form of authentication included in the URL.
        /// </summary>
        [Persistent]
        public string ServerUrl { get; set; }

        /// <summary>
        /// Gets or sets the username.
        /// </summary>
        [Persistent]
        public string Username { get; set; }

        /// <summary>
        /// Gets or sets the password.
        /// </summary>
        [Persistent]
        public string Password { get; set; }

        /// <summary>
        /// The delay in seconds when triggering a build before checking to make sure it starts
        /// </summary>
        [Persistent]
        public int Delay { get; set; }

        /// <summary>
        /// Gets the base URL used for connections to the Jenkins server that incorporates the authentication mechanism.
        /// </summary>
        public string BaseUrl
        {
            get
            {
                return this.ServerUrl.TrimEnd('/');
            }
        }

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String"/> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return string.Empty;
        }
        
        internal static JenkinsConfigurer GetConfigurer(string profileName = null)
        {
            var typ = typeof(JenkinsConfigurer);

            var profiles = StoredProcs
                .ExtensionConfiguration_GetConfigurations(typ.FullName + "," + typ.Assembly.GetName().Name)
                .Execute();

            var configurer =
                profiles.FirstOrDefault(p => string.Equals(profileName, p.Profile_Name, System.StringComparison.OrdinalIgnoreCase)) ??
                    profiles.FirstOrDefault(p => p.Default_Indicator.Equals(Domains.YN.Yes));

            if (configurer == null) 
                return null;

            return (JenkinsConfigurer)Util.Persistence.DeserializeFromPersistedObjectXml(configurer.Extension_Configuration);
        }
    }
}
