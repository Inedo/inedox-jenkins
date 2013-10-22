using System;
using System.Linq;
using System.Web.UI.WebControls;
using System.Xml.Linq;
using Inedo;
using Inedo.BuildMasterExtensions.Jenkins;
using Inedo.Web.Controls;
using RestSharp;

namespace Jenkins
{
    internal sealed class JenkinsJobPicker : ComboSelect
    {
        internal void FillItems(string configurationProfileName)
        {
            var configurer = JenkinsConfigurer.GetConfigurer(InedoLib.Util.NullIf(configurationProfileName, string.Empty));
            if (configurer == null) 
                return;

            var client = new JenkinsActionBase.JenkinsClient(configurer);
            client.Client.FollowRedirects = false;
            var request = new RestRequest("view/All/api/xml", Method.GET);
            var resp = client.Client.Execute(request);
            if (resp.StatusCode != System.Net.HttpStatusCode.OK)
                throw new Exception("Expected Status Code: 200, received: " + resp.StatusCode);

            var jobs = XDocument.Parse(resp.Content).Element("allView").Elements("job").Select(x => x.Element("name").Value);
            this.Items.AddRange(jobs.Select(j => new ListItem(j)).ToArray());
        }
    }
}
