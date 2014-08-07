using System;
using System.Linq;
using System.Web.UI;
using Inedo.BuildMaster;
using Inedo.BuildMaster.Data;
using Inedo.BuildMaster.Extensibility.Recipes;
using Inedo.BuildMaster.Web.Controls;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.Web.Controls;
using Inedo.Web.Controls.SimpleHtml;

namespace Inedo.BuildMasterExtensions.Jenkins
{
    internal sealed class DeployJenkinsBuildRecipeEditor : RecipeEditorBase
    {
        private sealed class DeployJenkinsRecipeWizardSteps : RecipeWizardSteps
        {
            public RecipeWizardStep About = new RecipeWizardStep("About");
            public RecipeWizardStep JenkinsConnection = new RecipeWizardStep("Jenkins Server");
            public RecipeWizardStep JenkinsBuild = new RecipeWizardStep("Select Job");
            public RecipeWizardStep SelectDeploymentPath = new RecipeWizardStep("Deployment Target");
            public RecipeWizardStep Summary = new RecipeWizardStep("Summary");

            public override RecipeWizardStep[] WizardStepOrder
            {
                get
                {
                    return new[] { this.About, this.JenkinsConnection, this.JenkinsBuild, base.SpecifyApplicationProperties, base.SpecifyWorkflowOrder, this.SelectDeploymentPath, this.Summary };
                }
            }
        }

        private int TargetServerId
        {
            get { return (int)(this.ViewState["TargetServerId"] ?? 0); }
            set { this.ViewState["TargetServerId"] = value; }
        }

        private string TargetDeploymentPath
        {
            get { return (string)this.ViewState["TargetDeploymentPath"]; }
            set { this.ViewState["TargetDeploymentPath"] = value; }
        }

        private string Job
        {
            get { return (string)this.ViewState["Job"]; }
            set { this.ViewState["Job"] = value; }
        }
        private DeployJenkinsRecipeWizardSteps wizardSteps = new DeployJenkinsRecipeWizardSteps();

        public override bool DisplayAsWizard { get { return true; } }

        public override RecipeWizardSteps GetWizardStepsControl()
        {
            return this.wizardSteps;
        }

        public override string DefaultNewApplicationName
        {
            get
            {
                return InedoLib.Util.CoalesceStr(this.Job, base.DefaultNewApplicationName);
            }
        }

        protected override void CreateChildControls()
        {
            base.CreateChildControls();

            this.CreateAboutControls();
            this.CreateJenkinsConnectionControls();
            this.CreateSelectArtifactControls();
            this.CreateSelectDeploymentPathControls();
            this.CreateSummaryControls();
        }

        private void CreateSummaryControls()
        {
            this.wizardSteps.Summary.Controls.Add(
                new FormFieldGroup(
                    "Summary",
                    "This is a summary of the Deploy Build from Jenkins wizard application - once created, you can change it to customize it however you'd like.",
                    true,
                    new StandardFormField("", new Summary(this))
                )
            );
        }

        private void CreateAboutControls()
        {
            this.wizardSteps.About.Controls.Add(
                new H2("About the ", new I("Deploy Jenkins Build") ," Wizard"),
                new P(
                    "This wizard will create a basic application that imports build artifacts from a job and then deploys those to a target folder. ",
                    "It's meant to be a starting point and, once the wizard completes, you can add additional actions to the deployment plan that can ",
                    "do all sorts of things, such as deploying to multiple servers, stopping/starting service, etc."
                ),
                new P(
                    "To learn more about BuildMaster integration, see the ",
                    new A("Jenkins Extension") { Href = "http://inedo.com/buildmaster/extensions/jenkins", Target = "_blank" },
                    " for more details."
                )
            );
        }

        private void CreateJenkinsConnectionControls()
        {
            var defaultCfg = (JenkinsConfigurer)this.GetExtensionConfigurer();
            var ctlError = new InfoBox { BoxType = InfoBox.InfoBoxTypes.Error, Visible = false };

            var txtServerUrl = new ValidatingTextBox
            {
                Required = true,
                Text = defaultCfg.ServerUrl,
                Width = 350
            };

            var txtUsername = new ValidatingTextBox
            {
                Text = defaultCfg.Username,
                Width = 350
            };
            var txtPassword = new PasswordTextBox
            {
                Text = defaultCfg.Password,
                Width = 350
            };

            txtServerUrl.ServerValidate += (s, e) =>
            {
                var configurer = new JenkinsConfigurer
                {
                    ServerUrl = txtServerUrl.Text,
                    Username = txtUsername.Text,
                    Password = txtPassword.Text
                };
                try
                {
                    
                }
                catch (Exception ex)
                {
                    e.IsValid = false;
                    ctlError.Visible = true;
                    ctlError.Controls.Add(new P("An error occurred while attempting to connect: " + ex.Message));
                }
            };

            this.wizardSteps.JenkinsConnection.Controls.Add(
                ctlError,
                new FormFieldGroup(
                    "Jenkins Server URL",
                    "Enter the URL of the Jenkins server, typically: http://jenkinsserver",
                    false,
                    new StandardFormField("Server URL:", txtServerUrl)
                ),
                new FormFieldGroup(
                    "Authentication",
                    "For HTTP Authentication, please enter the user credentials. Leaving the username blank will use anonymous authentication. For Jenkins version 1.426 and higher enter the API Token value as the password.",
                    true,
                    new StandardFormField("Username:", txtUsername),
                    new StandardFormField("Password:", txtPassword)
                )
            );
            this.WizardStepChange += (s, e) =>
            {
                if (e.CurrentStep != this.wizardSteps.JenkinsConnection) return;

                var qualifiedTypeName = typeof(JenkinsConfigurer).FullName + "," + typeof(JenkinsConfigurer).Assembly.GetName().Name;

                defaultCfg.ServerUrl = txtServerUrl.Text;
                defaultCfg.Username = txtUsername.Text;
                defaultCfg.Password = txtPassword.Text;
                var defaultProfile = StoredProcs
                        .ExtensionConfiguration_GetConfigurations(qualifiedTypeName)
                        .Execute()
                        .Where(p => p.Default_Indicator == Domains.YN.Yes)
                        .FirstOrDefault() ?? new Tables.ExtensionConfigurations();

                StoredProcs
                    .ExtensionConfiguration_SaveConfiguration(
                        Util.NullIf(defaultProfile.ExtensionConfiguration_Id, 0),
                        qualifiedTypeName,
                        defaultProfile.Profile_Name ?? "Default",
                        Util.Persistence.SerializeToPersistedObjectXml(defaultCfg),
                        Domains.YN.Yes)
                    .Execute();
            };
        }

        private void CreateSelectArtifactControls()
        {
            var ctlJenkinsJobPicker = new JenkinsJobPicker((JenkinsConfigurer)this.GetExtensionConfigurer());
            
            this.wizardSteps.JenkinsBuild.Controls.Add(
                new FormFieldGroup(
                    "Job name",
                    "This is the job where artifacts will be retrieved from.",
                    false,
                    new StandardFormField("Job name:", ctlJenkinsJobPicker)
                )
            );
            this.WizardStepChange += (s, e) =>
            {
                if (e.CurrentStep != this.wizardSteps.JenkinsBuild) return;
                this.Job = ctlJenkinsJobPicker.SelectedValue;
            };
        }

        private void CreateSelectDeploymentPathControls()
        {
            var ctlTargetDeploymentPath = new SourceControlFileFolderPicker()
            {
                DisplayMode = SourceControlBrowser.DisplayModes.Folders,
                ServerId = 1,
                Text = @"C:\JenkinsTestDeploys\" + this.Job,
                Width = 300
            };

            this.wizardSteps.SelectDeploymentPath.Controls.Add(
                new FormFieldGroup(
                    "Deployment Target",
                    "Select a directory where the artifact will be deployed. This is mostly for demonstrative purposes, and can be changed to a different location and server later.",
                    true,
                    new StandardFormField("Target Directory:", ctlTargetDeploymentPath)
                )
            );
            this.WizardStepChange += (s, e) =>
            {
                if (e.CurrentStep != this.wizardSteps.SelectDeploymentPath)
                    return;
                this.TargetDeploymentPath = ctlTargetDeploymentPath.Text;
            };
        }

        public override RecipeBase CreateFromForm()
        {
            return new DeployJenkinsBuildRecipe()
            {
                TargetDeploymentPath = this.TargetDeploymentPath,
                Job = this.Job
            };
        }

        private sealed class Summary : Control
        {
            private DeployJenkinsBuildRecipeEditor editor;

            public Summary(DeployJenkinsBuildRecipeEditor editor)
            {
                this.editor = editor;
            }

            protected override void Render(HtmlTextWriter writer)
            {
                if (editor.TargetDeploymentPath == null || string.IsNullOrEmpty(editor.Job))
                    return;

                writer.Write(
                    "<p><strong>Jenkins Job: </strong> {0}</p>" +
                    "<p><strong>Deployment Target Path: </strong> {1}</p>",
                    editor.Job,
                    editor.TargetDeploymentPath
                );
            }
        }
    }
}
