using Inedo.BuildMaster.Documentation;
using Inedo.BuildMaster.Extensibility.BuildImporters;
using Inedo.BuildMaster.Web;
using Inedo.Serialization;

namespace Inedo.BuildMasterExtensions.Jenkins
{
    [CustomEditor(typeof(JenkinsBuildImporterTemplateEditor))]
    public sealed class JenkinsBuildImporterTemplate : BuildImporterTemplateBase<JenkinsBuildImporter>
    {
        public override RichDescription GetDescription()
        {
            var desc = new RichDescription("Import ");
            if (!string.IsNullOrEmpty(this.BuildNumber))
                desc.AppendContent(new Hilite(this.BuildNumber));

            desc.AppendContent(" from ", new Hilite(this.JobName));

            return desc;
        }

        [Persistent]
        public string ArtifactName { get; set; }

        [Persistent]
        public string BuildNumber { get; set; }

        [Persistent]
        public string JobName { get; set; }
    }
}
