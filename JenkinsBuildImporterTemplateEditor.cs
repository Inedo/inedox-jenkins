using System.Web.UI.WebControls;
using Inedo.BuildMaster;
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
                    new ListItem("allow entry at build time", ""),
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
            var templat = (JenkinsBuildImporterTemplate)extension;
            this.txtArtifactName.Text = templat.ArtifactName;
            this.txtJobName.Text = templat.JobName;
            this.ddlBuildNumber.SelectedValue = templat.BuildNumber;
        }

        public override BuildImporterTemplateBase CreateFromForm()
        {
            return new JenkinsBuildImporterTemplate
            {
                ArtifactName = Util.NullIf(txtArtifactName.Text, ""),
                BuildNumber = Util.NullIf(ddlBuildNumber.SelectedValue, ""),
                JobName = txtJobName.Text
            };
        }
    }
}
