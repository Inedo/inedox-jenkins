using System.Web.UI.WebControls;
using Inedo.BuildMaster.Extensibility.BuildImporters;
using Inedo.BuildMaster.Web.Controls.Extensions.BuildImporters;
using Inedo.Web.Controls;

namespace Inedo.BuildMasterExtensions.Jenkins
{
    internal sealed class JenkinsBuildImporterTemplateEditor : BuildImporterTemplateEditorBase
    {
        private JenkinsJobPicker txtJobName;
        private DropDownList ddlBuildNumber;
        private ValidatingTextBox txtArtifactName;

        protected override void CreateChildControls()
        {
            this.txtJobName = new JenkinsJobPicker((JenkinsConfigurer)this.GetExtensionConfigurer());
            this.ddlBuildNumber = new DropDownList
            {
                Items =
                {
                    new ListItem("allow entry at build import time", ""),
                    new ListItem("lastBuild"),
                    new ListItem("lastCompletedBuild"),
                    new ListItem("lastStableBuild"),
                    new ListItem("lastSuccessfulBuild"),
                }
            };
            this.txtArtifactName = new ValidatingTextBox { DefaultText = "same as job name" };

            this.Controls.Add(
                new SlimFormField("Job name:", this.txtJobName),
                new SlimFormField("Build number:", this.ddlBuildNumber),
                new SlimFormField("Create as artifact:", txtArtifactName) { HelpText = "All artifacts from the specified job will be captured as a zip file and then saved to this artifact in BuildMaster." }
            );
        }

        public override void BindToForm(BuildImporterTemplateBase extension)
        {
            var template = (JenkinsBuildImporterTemplate)extension;
            this.txtArtifactName.Text = template.ArtifactName;
            this.txtJobName.Text = template.JobName;
            this.ddlBuildNumber.SelectedValue = template.BuildNumber;
        }

        public override BuildImporterTemplateBase CreateFromForm()
        {
            return new JenkinsBuildImporterTemplate
            {
                ArtifactName = AH.NullIf(txtArtifactName.Text, ""),
                BuildNumber = AH.NullIf(ddlBuildNumber.SelectedValue, ""),
                JobName = txtJobName.Text
            };
        }
    }
}
