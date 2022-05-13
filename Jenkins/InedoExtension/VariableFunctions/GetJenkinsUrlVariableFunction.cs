using System;
using System.ComponentModel;
using Inedo.Documentation;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensibility.VariableFunctions;
using Inedo.Extensions.Jenkins.Credentials;

namespace Inedo.Extensions.Jenkins.VariableFunctions
{
    [ScriptAlias("JenkinsUrl")]
    [ScriptAlias("GetJenkinsUrl")]
    [Description("The url for the Jenkins Build")]
    [Example(@"# 
$GetJenkinsBuildUrl(Jenkins, $JenkinsJobName);
$GetJenkinsBuildUrl(Jenkins, $JenkinsJobName, $JenkinsBuildNumber, $JenkinsBranchName);
")]
    public sealed class GetJenkinsUrlVariableFunction : ScalarVariableFunction
    {
        [VariableFunctionParameter(0)]
        [ScriptAlias("resource")]
        [Description("The name of the Jenkins secure resource to read.")]
        public string ResourceName { get; set; }

        [VariableFunctionParameter(1)]
        [ScriptAlias("jobName")]
        [Description("The Jenkins job name.")]
        public string JobName { get; set; }

        [VariableFunctionParameter(2, Optional = true)]
        [ScriptAlias("buildNumber")]
        [Description("The Jenkins build number.")]
        public string BuildNumber { get; set; } = null;

        [VariableFunctionParameter(3, Optional = true)]
        [ScriptAlias("branchName")]
        [Description("The Jenkins branch name.")]
        public string BranchName { get; set; } = null;

        protected override object EvaluateScalar(IVariableFunctionContext context)
        {
            var resource = SecureResource.TryCreate(this.ResourceName, new ResourceResolutionContext(context.ProjectId)) as JenkinsSecureResource;
            if (resource == null)
                throw new ExecutionFailureException($"Could not find a Jenkins Secure Resource named \"{this.ResourceName}\"; this error may occur if you renamed a credential, or the application or environment in context does not match any existing credentials. To resolve, edit this item, property, or operation's configuration, ensure a valid credential for the application/environment in context is selected, and then save.");

            UriBuilder uri = new UriBuilder(resource.ServerUrl)
            {
                Path = JenkinsClient.GetPath(this.JobName, this.BranchName, this.BuildNumber)
            };

            return uri.ToString();
        }
    }
}
