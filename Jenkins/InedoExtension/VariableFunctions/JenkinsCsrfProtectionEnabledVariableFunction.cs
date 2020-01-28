using System.ComponentModel;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.Jenkins.VariableFunctions
{
    [ExtensionConfigurationVariable]
    [ScriptAlias("JenkinsCsrfProtectionEnabled")]
    [AppliesTo(InedoProduct.BuildMaster | InedoProduct.Otter)]
    [Description("The default value for CsrfProtectionEnabled on Jenkins operations. Defaults to true.")]
    [DefaultValue(true)]
    public sealed class JenkinsCsrfProtectionEnabledVariableFunction : ScalarVariableFunction
    {
        protected override object EvaluateScalar(IVariableFunctionContext context) => true;
    }
}
