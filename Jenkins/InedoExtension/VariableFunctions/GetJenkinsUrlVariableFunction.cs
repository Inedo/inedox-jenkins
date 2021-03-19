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
        [ScriptAlias("credential")]
        [Description("The name of the credential to read, optionally prefixed with the type name of the credential separated by the scope resolution operator (i.e. ::).")]
        public string CredentialName { get; set; }

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
            var name = Inedo.Extensibility.Credentials.CredentialName.TryParse(this.CredentialName);
            if (name == null)
                throw new ExecutionFailureException(true, $"The specified credential name \"{this.CredentialName}\" is invalid.");

            // need to resolve credential type name if it's not specified with the scope resolution operator
            if (name.TypeName == null)
            {
                var types = (from c in SDK.GetCredentials()
                             where string.Equals(c.Name, name.Name, StringComparison.OrdinalIgnoreCase)
                             select c.LegacyResourceCredentialTypeName).ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (types.Count == 0)
                    throw new ExecutionFailureException(true, $"There are no credentials named \"{name.Name}\" found in the system.");
                if (types.Count > 1)
                    throw new ExecutionFailureException(true, $"There are multiple credential types with the name \"{name.Name}\" found in the system. Use the scope resolution operator (i.e. ::) to specify a type, for example: UsernamePassword::{name.Name}");

                name = new CredentialName(types.First(), name.Name);
            }

            var credential = ResourceCredentials.TryCreate(name.TypeName, name.Name, environmentId: context.EnvironmentId, applicationId: context.ProjectId, inheritFromParent: true);

            if (credential == null)
                throw new ExecutionFailureException($"Could not find a {name.TypeName} Resource Credentials named \"{name.Name}\"; this error may occur if you renamed a credential, or the application or environment in context does not match any existing credentials. To resolve, edit this item, property, or operation's configuration, ensure a valid credential for the application/environment in context is selected, and then save.");

            if (!(credential is JenkinsLegacyCredentials))
            {
                throw new ExecutionFailureException($"Resource Credential \"{name.Name}\" is not a Jenkins Credential.");
            }

            var jenkins = (JenkinsLegacyCredentials)credential;

            UriBuilder uri = new UriBuilder(jenkins.ServerUrl)
            {
                Path = JenkinsClient.GetPath(this.JobName, this.BranchName, this.BuildNumber)
            };

            return uri.ToString();
        }
    }
}
