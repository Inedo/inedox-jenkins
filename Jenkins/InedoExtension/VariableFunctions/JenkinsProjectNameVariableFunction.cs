using System.ComponentModel;
using Inedo.Documentation;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensibility.VariableFunctions;
using Inedo.Extensions.Jenkins.Credentials;

namespace Inedo.Extensions.Jenkins.VariableFunctions;

[ScriptAlias("JenkinsProjectName")]
[Description("The project/job name associated with a given Jenkins resource")]
[Example(@"# 
$JenkinsProjectName(MyJenkinsResourceName);
$JenkinsProjectName($CIProject);
")]
public sealed class JenkinsProjectNameVariableFunction : ScalarVariableFunction
{
    [VariableFunctionParameter(0)]
    [ScriptAlias("resource")]
    [Description("The name of the Jenkins resource to read.")]
    public string? ResourceName { get; set; }

    protected override object EvaluateScalar(IVariableFunctionContext context)
    {
        if (string.IsNullOrEmpty(this.ResourceName))
            return null!;
        if (SecureResource.TryCreate(SecureResourceType.CIProject, this.ResourceName, new ResourceResolutionContext(context.ProjectId)) is not JenkinsProject project)
            throw new ExecutionFailureException($"Could not find a Jenkins resource named \"{this.ResourceName}\"; this error may occur if the BuildMaster build is not associated with a Jenkins build, or if you renamed a resource.");

        return project.ProjectId!;
    }
}
