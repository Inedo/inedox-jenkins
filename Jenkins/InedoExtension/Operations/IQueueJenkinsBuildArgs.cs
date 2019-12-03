using Inedo.Diagnostics;
using Inedo.Extensibility.Operations;

namespace Inedo.Extensions.Jenkins.Operations
{
    internal interface IQueueJenkinsBuildArgs : ILogSink, IJenkinsConnectionInfo
    {
        string JobName { get; set; }
        string BranchName { get; set; }
        string AdditionalParameters { get; set; }
        bool WaitForStart { get; set; }
        bool WaitForCompletion { get; set; }
        bool ProxyRequest { get; set; }
        string JenkinsBuildNumber { get; set; }

        void SetProgress(OperationProgress progress);
    }
}
