using System;
using System.Linq;
using System.Web.UI.WebControls;
using Inedo.BuildMaster.Extensibility;
using Inedo.Diagnostics;
using Inedo.Web.Controls;

namespace Inedo.BuildMasterExtensions.Jenkins
{
    internal sealed class JenkinsJobPicker : ComboSelect
    {
        private class DummyLogger : ILogger
        {
            public void Log(MessageLevel logLevel, string message)
            {
            }
        }
        public JenkinsJobPicker(JenkinsConfigurer configurer)
        {
            if (configurer == null) return;

            this.Init += (s,e) => 
                this.Items.AddRange(
                    new JenkinsClient(configurer, new DummyLogger())
                    .GetJobNames()
                    .Select(j => new ListItem(j)));
        }
    }
}