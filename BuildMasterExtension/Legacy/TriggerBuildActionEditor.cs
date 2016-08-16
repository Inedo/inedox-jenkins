using System.Web.UI.WebControls;
using Inedo.BuildMaster.Extensibility.Actions;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.Web.Controls;

namespace Inedo.BuildMasterExtensions.Jenkins
{
    internal sealed class TriggerBuildActionEditor : ActionEditorBase
    {
        private ValidatingTextBox txtJobName;
        private ValidatingTextBox txtAdditionalParameters;
        private CheckBox chkWaitForCompletion;

        public override void BindToForm(ActionBase extension)
        {
            var action = (TriggerBuildAction)extension;

            this.txtJobName.Text = action.JobName;
            this.txtAdditionalParameters.Text = action.AdditionalParameters;
            this.chkWaitForCompletion.Checked = action.WaitForCompletion;
        }

        public override ActionBase CreateFromForm()
        {
            return new TriggerBuildAction
            {
                JobName = this.txtJobName.Text,
                AdditionalParameters = this.txtAdditionalParameters.Text,
                WaitForCompletion = this.chkWaitForCompletion.Checked
            };
        }

        protected override void CreateChildControls()
        {
            this.txtJobName = new ValidatingTextBox { Required = true };
            this.txtAdditionalParameters = new ValidatingTextBox { DefaultText = "none" };
            this.chkWaitForCompletion = new CheckBox { Text = "Wait for build to complete", Checked = true };

            this.Controls.Add(
                new SlimFormField("Job name:", this.txtJobName),
                new SlimFormField("Build parameters:", this.txtAdditionalParameters),
                new SlimFormField("Build options:", this.chkWaitForCompletion)
            );
        }
    }
}
