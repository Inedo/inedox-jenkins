using System;
using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.Extensions.Jenkins.Operations;

namespace Inedo.BuildMasterExtensions.Jenkins
{
    internal sealed class GetArtifactActionConverter : IActionOperationConverter<GetArtifactAction, DownloadJenkinsArtifactOperation>
    {
        public ConvertedOperation<DownloadJenkinsArtifactOperation> ConvertActionToOperation(GetArtifactAction action, IActionConverterContext context)
        {
            var config = (JenkinsConfigurer)context.Configurer;
            return new DownloadJenkinsArtifactOperation
            {
                ServerUrl = config.ServerUrl,
                UserName = config.Username,
                Password = config.Password,
                BuildNumber = action.BuildNumber,
                ArtifactName = action.ArtifactName,
                JobName = action.JobName,
                ExtractFilesToTargetDirectory = action.ExtractFilesToTargetDirectory,
                TargetDirectory = action.OverriddenTargetDirectory
            };
        }
    }
}