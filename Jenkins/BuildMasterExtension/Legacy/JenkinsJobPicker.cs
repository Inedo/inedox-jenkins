using System.Linq;
using System.Web.UI.WebControls;
using Inedo.Web.Controls;

namespace Inedo.BuildMasterExtensions.Jenkins
{
    internal sealed class JenkinsJobPicker : ValidatingTextBox
    {
        public JenkinsJobPicker(JenkinsConfigurer configurer)
        {
            //if (configurer == null)
            //    return;

            //this.Init += (s,e) => 
            //    this.Items.AddRange(
            //        new JenkinsClient(configurer)
            //        .GetJobNames()
            //        .Select(j => new ListItem(j)));
        }
    }
}