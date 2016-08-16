using Inedo.BuildMaster.Extensibility.Configurers.Extension;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.Web.Controls;

namespace Inedo.BuildMasterExtensions.Jenkins
{
    internal sealed class JenkinsConfigurerEditor : ExtensionConfigurerEditorBase
    {
        private ValidatingTextBox txtServerUrl;
        private ValidatingTextBox txtUsername;
        private PasswordTextBox txtPassword;

        public override void InitializeDefaultValues()
        {
        }

        public override void BindToForm(ExtensionConfigurerBase extension)
        {
            var configurer = (JenkinsConfigurer)extension;

            this.txtServerUrl.Text = configurer.ServerUrl;
            if (!string.IsNullOrEmpty(configurer.Username))
            {
                this.txtUsername.Text = configurer.Username;
                this.txtPassword.Text = configurer.Password;
            }
        }


        public override ExtensionConfigurerBase CreateFromForm()
        {
            var configurer = new JenkinsConfigurer
            {
                ServerUrl = this.txtServerUrl.Text
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
            this.txtServerUrl = new ValidatingTextBox { Required = true };
            this.txtUsername = new ValidatingTextBox { DefaultText = "Anonymous" };
            this.txtPassword = new PasswordTextBox();

            this.Controls.Add(
                new SlimFormField("Server URL:", this.txtServerUrl)
                { HelpText = "This should be the base url that you use to access Jenkins; for example, http://myjenkinsbox.local:8080" },
                new SlimFormField("Username:", this.txtUsername),
                new SlimFormField("API Token / Password", this.txtPassword) { HelpText = "For Jenkins version 1.426 and higher enter the API Token value as the password" }
            );
        }
    }
}
