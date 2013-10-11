using Inedo.BuildMaster.Extensibility.Configurers.Extension;
using Inedo.BuildMaster.Web.Controls;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.Web.Controls;

namespace Inedo.BuildMasterExtensions.Jenkins
{
    internal sealed class JenkinsConfigurerEditor : ExtensionConfigurerEditorBase
    {
        private ValidatingTextBox txtServerUrl;
        private ValidatingTextBox txtUsername;
        private PasswordTextBox txtPassword;
        private ValidatingTextBox txtDelay;

        /// <summary>
        /// Initializes the default values.
        /// </summary>
        public override void InitializeDefaultValues()
        {
            BindToForm(new JenkinsConfigurer());
        }

        /// <summary>
        /// Binds to form.
        /// </summary>
        /// <param name="extension">The extension.</param>
        public override void BindToForm(ExtensionConfigurerBase extension)
        {
            var configurer = (JenkinsConfigurer)extension;

            this.txtServerUrl.Text = configurer.ServerUrl;
            this.txtDelay.Text = configurer.Delay.ToString();
            if (!string.IsNullOrEmpty(configurer.Username))
            {
                this.txtUsername.Text = configurer.Username;
                this.txtPassword.Text = configurer.Password;
            }
        }

        /// <summary>
        /// Creates from form.
        /// </summary>
        /// <returns></returns>
        public override ExtensionConfigurerBase CreateFromForm()
        {
            var configurer = new JenkinsConfigurer()
            {
                ServerUrl = this.txtServerUrl.Text,
                Delay = int.Parse(this.txtDelay.Text) 
            };
            if (!string.IsNullOrEmpty(this.txtUsername.Text))
            {
                configurer.Username = this.txtUsername.Text;
                configurer.Password = this.txtPassword.Text;
            }

            return configurer;
        }

        protected override void CreateChildControls()
        {
            this.txtServerUrl = new ValidatingTextBox()
            {
                Required = true,
                Width = 300
            };

            this.txtUsername = new ValidatingTextBox()
            {
                Width = 300
            };

            this.txtPassword = new PasswordTextBox()
            {
                Width = 270
            };
            this.txtDelay = new ValidatingTextBox() { Width = 300, Required = true, Text = "10" };
            CUtil.Add(this,
                new FormFieldGroup(
                    "Jenkins Server URL",
                    "Enter the URL of the Jenkins server, typically: http://server",
                    false,
                    new StandardFormField("Server URL:", this.txtServerUrl)
                ),
                new FormFieldGroup(
                    "Authentication",
                    "For HTTP Authentication, please enter the user credentials. Leaving the username blank will use anonymous authentication. For Jenkins version 1.426 and higher enter the API Token value as the password.",
                    true,
                    new StandardFormField("Username:", this.txtUsername),
                    new StandardFormField("Password:", this.txtPassword)
                ),
                new FormFieldGroup(
                    "Server Options",
                    "Set the delay (quiet period) in seconds before a just triggered build is first checked for its status",
                    false,
                    new StandardFormField("Build Delay:", this.txtDelay )
                )
            );
        }
    }
}
