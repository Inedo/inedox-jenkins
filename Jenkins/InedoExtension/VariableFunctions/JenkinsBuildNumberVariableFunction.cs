using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.Jenkins.VariableFunctions;

[ScriptAlias("JenkinsBuildNumber")]
[Description("Returns the build number portion of a multipart buildId, or an empty string if there is no branch portion")]
[Example(@"# 
$JenkinsBuildNumber(my-branch-18) ==> ""18""
$JenkinsBuildNumber ==> """"
$JenkinsBuildNumber(5214) ==> ""5214""
$JenkinsBuildNumber(latestBuild) ==> error (invalid BuildId)
")]
public sealed class JenkinsBuildNumberVariableFunction : ScalarVariableFunction
{
    [VariableFunctionParameter(0)]
    [ScriptAlias("id")]
    [Description("The buildId to parse.")]
    public string? BuildId { get; set; }

    protected override object EvaluateScalar(IVariableFunctionContext context)
    {
        if (string.IsNullOrEmpty(this.BuildId))
            return null!;

        JenkinsClient.ParseBuildId(this.BuildId, out var _, out var buildNumber);
        return buildNumber.ToString();
    }
}
