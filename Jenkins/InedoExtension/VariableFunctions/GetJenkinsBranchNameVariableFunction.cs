using System;
using System.ComponentModel;
using System.Linq;
using System.Security;
using Inedo.Documentation;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.VariableFunctions;
using Inedo.Extensions.Jenkins.Credentials;
using Inedo.Serialization;

namespace Inedo.Extensions.Jenkins.VariableFunctions
{
    [ScriptAlias("JenkinsBranchName")]
    [Description("Returns a empty string in the event there is no overriding variable")]
    [Example(@"# 
JenkinsBranchName
")]
    public sealed class GetJenkinsBranchNameVariableFunction : ScalarVariableFunction
    {
        
        protected override object EvaluateScalar(IVariableFunctionContext context)
        {
            return String.Empty;
        }
    }
}
