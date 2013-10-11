using System.Web.UI.WebControls;
using Inedo.BuildMaster.Extensibility.Actions;
using Inedo.BuildMaster.Web.Controls;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.Web.Controls;

namespace Inedo.BuildMasterExtensions.Jenkins
{
    internal sealed class GetArtifactActionEditor : ActionEditorBase
    {
        private ValidatingTextBox txtArtifactName;
        private ValidatingTextBox txtJob;
        private ValidatingTextBox txtBuildNumber;
        private CheckBox chkExtractFilesToTargetDirectory;

        /// <summary>
        /// Gets a value indicating whether [display target directory].
        /// </summary>
        /// <value>
        /// 	<c>true</c> if [display target directory]; otherwise, <c>false</c>.
        /// </value>
        public override bool DisplayTargetDirectory { get { return true; } }

        /// <summary>
        /// Binds to form.
        /// </summary>
        /// <param name="extension">The extension.</param>
        public override void BindToForm(ActionBase extension)
        {
            var action = (GetArtifactAction)extension;

            this.txtArtifactName.Text = action.ArtifactName;
            this.txtJob.Text = action.Job;
            this.txtBuildNumber.Text = action.BuildNumber;
            this.chkExtractFilesToTargetDirectory.Checked = action.ExtractFilesToTargetDirectory;
        }

        /// <summary>
        /// Creates from form.
        /// </summary>
        /// <returns></returns>
        public override ActionBase CreateFromForm()
        {
            return new GetArtifactAction()
            {
                ArtifactName = this.txtArtifactName.Text,
                Job = this.txtJob.Text,
                BuildNumber = this.txtBuildNumber.Text,
                ExtractFilesToTargetDirectory = this.chkExtractFilesToTargetDirectory.Checked
            };
        }

        protected override void CreateChildControls()
        {
            this.txtArtifactName = new ValidatingTextBox()
            {
                Required = true,
                Width = 300
            };

            this.txtJob = new ValidatingTextBox()
            {
                Required = true
            };

            this.txtBuildNumber = new ValidatingTextBox()
            {
                Required = true
            };

            this.chkExtractFilesToTargetDirectory = new CheckBox()
            {
                Text = "Extract files in artifact to target directory"
            };

            CUtil.Add(this,
                new FormFieldGroup(
                    "Artifact Name",
                    "The name of artifact, for example: <br />\"foo.exe\". Use \"*\" to retrieve all artifacts for the build.",
                    false,
                    new StandardFormField("Artifact Name:", this.txtArtifactName)
                ),
                new FormFieldGroup(
                    "Job",
                    "This name of the job in Jenkins",
                    false,
                    new StandardFormField("Job ID:", this.txtJob)
                ),
                new FormFieldGroup(
                    "Build Number",
                    "The build number or one of these predefined constants: \"lastBuild\", \"lastCompletedBuild\", \"lastFailedBuild\", \"lastStableBuild\", \"lastSuccessfulBuild\" or \"lastUnsuccessfulBuild\".",
                    false,
                    new StandardFormField("Build Number:", this.txtBuildNumber)
                ),
                new FormFieldGroup(
                    "Additional Options",
                    "Select any addition options for this action.",
                    true,
                    new StandardFormField("", this.chkExtractFilesToTargetDirectory)
                )
            );
        }
    }
}
