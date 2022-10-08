using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.Jenkins.VariableFunctions;

[ScriptAlias("JenkinsBranchName")]
[Description("Returns the branch portion of a multi-part buildId, or an empty string if there is no branch portion")]
[Example(@"# 
$JenkinsBranchName(my-branch-18) ==> ""my-branch""
$JenkinsBranchName ==> """"
$JenkinsBranchName(5214) ==> """"
$JenkinsBranchName(lastestBuild) ==> error/invalid
")]
public sealed class JenkinsBranchNameVariableFunction : ScalarVariableFunction
{
    [VariableFunctionParameter(0)]
    [ScriptAlias("id")]
    [Description("The buildId to parse.")]
    public string? BuildId { get; set; }

    protected override object EvaluateScalar(IVariableFunctionContext context)
    {
        if (string.IsNullOrEmpty(this.BuildId))
            return null!;

        JenkinsClient.ParseBuildId(this.BuildId, out var branch, out var _);
        return branch!;
    }
}
