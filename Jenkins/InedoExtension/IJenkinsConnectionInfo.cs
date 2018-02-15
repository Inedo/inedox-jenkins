namespace Inedo.Extensions.Jenkins
{
    interface IJenkinsConnectionInfo
    {
        string ServerUrl { get; }
        string UserName { get; }
        string Password { get; }
    }

    internal static class IJenkinsConnectionInfoExtensions
    {
        public static string GetApiUrl(this IJenkinsConnectionInfo connectionInfo)
        {
            return connectionInfo.ServerUrl?.TrimEnd('/') ?? "";
        }
    }
}
