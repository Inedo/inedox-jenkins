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

        public override bool DisplayTargetDirectory { get { return true; } }

        public override void BindToForm(ActionBase extension)
        {
            var action = (GetArtifactAction)extension;

            this.txtArtifactName.Text = action.ArtifactName;
            this.txtJob.Text = action.JobName;
            this.txtBuildNumber.Text = action.BuildNumber;
            this.chkExtractFilesToTargetDirectory.Checked = action.ExtractFilesToTargetDirectory;
        }

        public override ActionBase CreateFromForm()
        {
            return new GetArtifactAction()
            {
                ArtifactName = this.txtArtifactName.Text,
                JobName = this.txtJob.Text,
                BuildNumber = this.txtBuildNumber.Text,
                ExtractFilesToTargetDirectory = this.chkExtractFilesToTargetDirectory.Checked
            };
        }

        protected override void CreateChildControls()
        {
            var client = (new JenkinsClient((JenkinsConfigurer)this.GetExtensionConfigurer()));

            this.txtArtifactName = new ValidatingTextBox { DefaultText = "download all artifacts" };

            this.txtJob = new ValidatingTextBox
            {
                Required = true,
                AutoCompleteValues = client.GetJobNames()
            };

            this.txtBuildNumber = new ValidatingTextBox 
            { 
                Required = true ,
                AutoCompleteValues = new [] { "lastBuild","lastCompletedBuild","lastStableBuild","lastSuccessfulBuild" }
            };

            this.chkExtractFilesToTargetDirectory = new CheckBox { Text = "Extract archive.zip when downloading all artifacts", Checked = true };

            this.Controls.Add(
                new SlimFormField("Artifact filter:", this.txtArtifactName),
                new SlimFormField("Job name:", this.txtJob),
                new SlimFormField("Build number", this.txtBuildNumber),
                new SlimFormField("Download options:", this.chkExtractFilesToTargetDirectory)
            );
        }
    }
}
