using Inedo.BuildMaster.Extensibility.BuildImporters;
using Inedo.BuildMaster.Web.Controls.Extensions.BuildImporters;
using Inedo.Web.Controls;

namespace Inedo.BuildMasterExtensions.Jenkins
{
    internal sealed class JenkinsBuildImporterEditor : BuildImporterEditorBase<JenkinsBuildImporterTemplate>
    {
        private ValidatingTextBox txtBuildNumber;

        protected override void CreateChildControls()
        {
            this.txtBuildNumber = new ValidatingTextBox
            {
                AutoCompleteValues = new[] { "lastBuild", "lastCompletedBuild", "lastStableBuild", "lastSuccessfulBuild" },
                DefaultText = "lastSuccessfulBuild",
                Required = true
            };

            this.Controls.Add(
                new SlimFormField("Job name:", this.Template.JobName),
                new SlimFormField("Jenkins build number:", this.txtBuildNumber) { Visible = string.IsNullOrEmpty(this.Template.BuildNumber) },
                new SlimFormField("Jenkins build number:", this.Template.BuildNumber ?? "") { Visible = !string.IsNullOrEmpty(this.Template.BuildNumber) }
            );
        }

        public override BuildImporterBase CreateFromForm()
        {
            string buildNumber = AH.CoalesceString(this.Template.BuildNumber, this.txtBuildNumber.Text, "lastSuccessfulBuild");

            return new JenkinsBuildImporter
            {
                ArtifactName = this.Template.ArtifactName ?? this.Template.JobName,
                BuildNumber = buildNumber,
                JobName = this.Template.JobName
            };
        }
    }
}
