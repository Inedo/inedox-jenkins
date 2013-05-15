using System.Web;
using System.Web.UI.WebControls;
using Inedo.BuildMaster.Extensibility.Actions;
using Inedo.BuildMaster.Web.Controls;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.Web.Controls;

namespace Inedo.BuildMasterExtensions.Jenkins
{
    internal sealed class TriggerBuildActionEditor : ActionEditorBase
    {
        private ValidatingTextBox txtJob;
        private ValidatingTextBox txtAdditionalParameters;
        private CheckBox chkWaitForCompletion;

        /// <summary>
        /// Binds to form.
        /// </summary>
        /// <param name="extension">The extension.</param>
        public override void BindToForm(ActionBase extension)
        {
            var action = (TriggerBuildAction)extension;

            this.txtJob.Text = action.Job;
            this.txtAdditionalParameters.Text = action.AdditionalParameters;
            this.chkWaitForCompletion.Checked = action.WaitForCompletion;
        }

        /// <summary>
        /// Creates from form.
        /// </summary>
        /// <returns></returns>
        public override ActionBase CreateFromForm()
        {
            return new TriggerBuildAction()
            {
                Job = this.txtJob.Text,
                AdditionalParameters = this.txtAdditionalParameters.Text,
                WaitForCompletion = this.chkWaitForCompletion.Checked
            };
        }

        protected override void CreateChildControls()
        {
            this.txtJob = new ValidatingTextBox()
            {
                Required = true
            };

            this.txtAdditionalParameters = new ValidatingTextBox()
            {
                Required = false,
                Width = 300
            };

            this.chkWaitForCompletion = new CheckBox()
            {
                Text = "Wait for build to complete",
                Checked = true
            };

            CUtil.Add(this, 
                new FormFieldGroup(
                    "Job ID",
                    "The name of the Job to build in Jenkins.", 
                    false, 
                    new StandardFormField("Build Configuration ID:", this.txtJob)
                ),
                new FormFieldGroup(
                    "Additional Parameters",
                    "Optionally enter any additional parameters to be passed to the build API call in query string format, for example:<br/> " + HttpUtility.HtmlEncode("&foo=bar&baz=boo"),
                    false,
                    new StandardFormField("Additional Parameters:", this.txtAdditionalParameters)
                ),
                new FormFieldGroup(
                    "Wait for Completion",
                    "Specify whether BuildMaster should pause the action until the Jenkins build has completed.",
                    true,
                    new StandardFormField("", this.chkWaitForCompletion)
                )
            );
        }
    }
}
