using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.Extensions.Jenkins.Operations;

namespace Inedo.BuildMasterExtensions.Jenkins
{
    internal sealed class TriggerBuildActionConverter : IActionOperationConverter<TriggerBuildAction, QueueJenkinsBuildOperation>
    {
        public ConvertedOperation<QueueJenkinsBuildOperation> ConvertActionToOperation(TriggerBuildAction action, IActionConverterContext context)
        {
            var config = (JenkinsConfigurer)context.Configurer;
            return new QueueJenkinsBuildOperation
            {
                ServerUrl = config.ServerUrl,
                UserName = config.Username,
                Password = config.Password,
                JobName = action.JobName,
                WaitForCompletion = action.WaitForCompletion,
            };
        }
    }
}