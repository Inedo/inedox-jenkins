using System.Security;
using System.Threading;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.Jenkins.Credentials;
using Inedo.Web;
using UsernamePasswordCredentials = Inedo.Extensions.Credentials.UsernamePasswordCredentials;

namespace Inedo.Extensions.Jenkins
{
    interface IJenkinsConnectionInfo
    {
        string ServerUrl { get; }
        string UserName { get; }
        SecureString Password { get; }
        bool CsrfProtectionEnabled { get; }
    }

    interface IJenkinsConfig
    {
        string ResourceName { get; }
        string ServerUrl { get; }
        string UserName { get; }
        SecureString Password { get; }
    }

    internal static class IJenkinsConnectionInfoExtensions
    {
        public static string GetApiUrl(this IJenkinsConnectionInfo connectionInfo) => GetApiUrl(connectionInfo.ServerUrl);

        public static string GetApiUrl(string serverUrl)
        {
            return serverUrl?.TrimEnd('/') ?? "";
        }

        public static JenkinsClient CreateClient(this ISuggestionProvider s, IComponentConfiguration config, CancellationToken token)
        {
            if (s == null)
                return null;

            var (c, r) = config.GetCredentialsAndResource();
            if (string.IsNullOrEmpty(r?.ServerUrl))
                return null;

            var up = c as Extensions.Credentials.UsernamePasswordCredentials;
            var api = c as Extensions.Credentials.TokenCredentials;
            var client = new JenkinsClient(
                up?.UserName,
                up?.Password ?? api?.Token,
                r?.ServerUrl,
                csrfProtectionEnabled: false,
                null,
                token
            );

            return client;
        }

        public static (SecureCredentials, JenkinsSecureResource) GetCredentialsAndResource(this IJenkinsConfig operation, IOperationExecutionContext context)
            => GetCredentialsAndResource(operation, (ICredentialResolutionContext)context);

        public static (SecureCredentials, JenkinsSecureResource) GetCredentialsAndResource(this IComponentConfiguration config)
        {
            int? projectId = AH.ParseInt(AH.CoalesceString(config["ProjectId"], config["ApplicationId"]));
            int? environmentId = AH.ParseInt(config["EnvironmentId"]);
            return GetCredentialsAndResource(new SuggestionProviderContext(config), new CredentialResolutionContext(projectId, environmentId));
        }

        public static (SecureCredentials, JenkinsSecureResource) GetCredentialsAndResource(this IJenkinsConfig operation, ICredentialResolutionContext context)
        {
            string username = null;
            SecureString passwordOrApiKey = null;

            JenkinsSecureResource resource;
            if (string.IsNullOrEmpty(operation.ResourceName))
            {
                username = operation.UserName;
                passwordOrApiKey = operation.Password;
                resource = string.IsNullOrEmpty(operation.ServerUrl) ? null : new JenkinsSecureResource();
            }
            else
            {
                resource = (JenkinsSecureResource)SecureResource.TryCreate(operation.ResourceName, context);
                if (resource != null)
                {
                    var credentials = resource.GetCredentials(context);
                    passwordOrApiKey = (credentials as TokenCredentials)?.Token ?? (credentials as UsernamePasswordCredentials)?.Password;
                    username = (credentials as UsernamePasswordCredentials)?.UserName;
                }
            }

            if (resource != null)
            {
                resource.ServerUrl = AH.CoalesceString(operation.ServerUrl, resource.ServerUrl);
            }

            if (string.IsNullOrEmpty(username))
                return (new TokenCredentials { Token = passwordOrApiKey }, resource);
            else
                return (new UsernamePasswordCredentials { UserName = username, Password = passwordOrApiKey }, resource);
        }

        private sealed class SuggestionProviderContext : IJenkinsConfig
        {
            private readonly IComponentConfiguration config;
            public SuggestionProviderContext(IComponentConfiguration config)
            {
                this.config = config;
            }

            public string ResourceName => AH.CoalesceString(this.config["ResourceName"], this.config["CredentialName"]);
            public string ServerUrl => this.config["ServerUrl"];
            public string UserName => this.config["UserName"];
            public SecureString Password => AH.CreateSecureString(this.config["Password"].ToString() ?? string.Empty);
        }
    }
}
