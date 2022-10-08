using System;
using System.ComponentModel;
using Inedo.Documentation;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensibility.VariableFunctions;
using Inedo.Extensions.Jenkins.Credentials;

namespace Inedo.Extensions.Jenkins.VariableFunctions;

[ScriptAlias("JenkinsUrl")]
[ScriptAlias("GetJenkinsUrl")]
[Description("The url for the Jenkins Build")]
[Example(@"# 
$JenkinsBuildUrl(MyJenkinsResource, $JenkinsJobName);
$JenkinsBuildUrl(MyJenkinsResource, $JenkinsJobName, $JenkinsBuildNumber, $JenkinsBranchName);
")]
public sealed class GetJenkinsUrlVariableFunction : ScalarVariableFunction
{
    [VariableFunctionParameter(0)]
    [ScriptAlias("resource")]
    [Description("The name of the Jenkins secure resource to read.")]
    public string? ResourceName { get; set; }

    [VariableFunctionParameter(1)]
    [ScriptAlias("projectName")]
    [Description("The Jenkins project name.")]
    public string? ProjectName { get; set; }

    [VariableFunctionParameter(2, Optional = true)]
    [ScriptAlias("buildNumber")]
    [Description("The Jenkins build number.")]
    public string? BuildNumber { get; set; }

    [VariableFunctionParameter(3, Optional = true)]
    [ScriptAlias("branchName")]
    [Description("The Jenkins branch name.")]
    public string? BranchName { get; set; }

    protected override object EvaluateScalar(IVariableFunctionContext context)
    {
        if (this.ProjectName == null)
            throw new ExecutionFailureException($"projectName is required.");

        var credCtx = new CredentialResolutionContext(context.ProjectId, null);
        if (SecureResource.TryCreate(this.ResourceName, credCtx) is not JenkinsProject project)
            throw new ExecutionFailureException($"Could not find a Jenkins resource named \"{this.ResourceName}\".");

        var url = project.LegacyServerUrl
            ?? (project.GetCredentials(credCtx) as JenkinsCredentials)?.ServiceUrl
            ?? throw new ExecutionFailureException($"Could not find a base url associated with \"{this.ResourceName}\".");

        var path = $"job/{Uri.EscapeDataString(this.ProjectName)}";
        
        if (!string.IsNullOrEmpty(this.BranchName))
            path += $"/job/{Uri.EscapeDataString(this.BranchName)}";
        if (!string.IsNullOrEmpty(this.BuildNumber))
            path += $"/{this.BuildNumber}";

        return new UriBuilder(url) { Path = path }.ToString();
    }
}
