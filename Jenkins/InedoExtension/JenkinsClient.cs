using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Inedo.Diagnostics;
using Inedo.Extensibility.CIServers;

namespace Inedo.Extensions.Jenkins;

internal sealed class JenkinsClient
{
    public static readonly string[] SpecialBuildNumbers = { "lastSuccessfulBuild", "lastStableBuild", "lastBuild", "lastCompletedBuild" };

    private readonly Func<ValueTask<HttpClient>> getHttpClientAsync;
    private readonly ILogSink? log;
    public JenkinsClient(JenkinsCredentials credentials, ILogSink? log = null)
    {
        if (string.IsNullOrEmpty(credentials.ServiceUrl))
            throw new ArgumentException($"{nameof(credentials.ServiceUrl)} is missing from Jenkins credentials.");
        this.log = log;

        var url = credentials.ServiceUrl;
        if (!url.EndsWith('/'))
            url += "/";

        var httpClient = SDK.CreateHttpClient();
        httpClient.BaseAddress = new Uri(url);

        string auth;
        if (!string.IsNullOrEmpty(credentials.UserName) && credentials.Password != null)
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("basic", Convert.ToBase64String(InedoLib.UTF8Encoding.GetBytes($"{credentials.UserName}:{AH.Unprotect(credentials.Password)}")));
            auth = $"Username \"{credentials.UserName}\"";
        }
        else
        {
            auth = "Anonymous";
        }

        this.getHttpClientAsync = async () =>
        {
            if (credentials.CsrfProtectionEnabled)
            {
                this.log?.LogDebug("Checking for CSRF protection...");
                using var response = await httpClient.GetAsync("/crumbIssuer/api/xml?xpath=concat(//crumbRequestField,\":\",//crumb)").ConfigureAwait(false);
                // Assume if the request failed that Jenkins is not set up to use CSRF protection.
                if (response.IsSuccessStatusCode)
                {
                    var csrfHeader = (await response.Content.ReadAsStringAsync().ConfigureAwait(false)).Split(new[] { ':' }, 2);
                    if (csrfHeader.Length == 2)
                        httpClient.DefaultRequestHeaders.Add(csrfHeader[0], csrfHeader[1]);
                }
            }
            return httpClient;
        };

        this.log?.LogDebug($"Initiating Jenkins connection as {auth} to {url}");
    }

    public async IAsyncEnumerable<CIProjectInfo> GetProjectsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var xdoc = await this.GetXDocumentAsync("api/xml", cancellationToken).ConfigureAwait(false);
        foreach (var jobElement in xdoc.Descendants("job"))
        {
            var name = (string?)jobElement.Element("name");
            if (!string.IsNullOrEmpty(name))
                yield return new CIProjectInfo(name);
        }
    }

    public IAsyncEnumerable<CIBuildInfo> GetBuildsAsync(string projectName, CancellationToken cancellationToken = default)
    {
        return this.GetBuildsInternalAsync($"job/{Uri.EscapeDataString(projectName)}/api/xml", cancellationToken);
    }
    public IAsyncEnumerable<CIBuildInfo> GetBuildsAsync(string projectName, string? branchName = null, CancellationToken cancellationToken = default)
    {
        var url = string.IsNullOrEmpty(branchName)
            ? $"job/{Uri.EscapeDataString(projectName)}/api/xml"
            : $"job/{Uri.EscapeDataString(projectName)}/job/{Uri.EscapeDataString(branchName)}api/xml";

        return this.GetBuildsInternalAsync(url, cancellationToken);
    }
    public async IAsyncEnumerable<string> GetBranchesAsync(string projectName, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var xdoc = await this.GetXDocumentAsync($"job/{Uri.EscapeDataString(projectName)}/api/xml", cancellationToken).ConfigureAwait(false);
        if (xdoc.Root == null)
            yield break;

        if (xdoc.Root.Name.LocalName == "workflowMultiBranchProject")
        {
            foreach (var branchElement in xdoc.Root.Descendants("job"))
            {
                var name = (string?)branchElement.Element("name");
                if (string.IsNullOrEmpty(name))
                    continue;

                var branchUrl = (string?)branchElement.Element("url");
                if (string.IsNullOrEmpty(branchUrl))
                    continue;

                yield return name;
            }
        }
        else
            yield return "";
    }

    public async IAsyncEnumerable<KeyValuePair<string, string>> GetBuildVariablesAsync(string projectName, string? branchName, int buildNumber, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var xdoc = await this.GetXDocumentAsync(GetBuildDetailsUrl(projectName, branchName, buildNumber), cancellationToken).ConfigureAwait(false);
        foreach (var p in xdoc.Descendants("parameter"))
        {
            var name = (string?)p.Element("name");
            var value = (string?)p.Element("value");

            if (name == null || value == null)
                continue;

            yield return new KeyValuePair<string, string>(name, value);
        }
    }

    public async IAsyncEnumerable<string> GetBuildArtifactsAsync(string projectName, string? branchName, int buildNumber, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var xdoc = await this.GetXDocumentAsync(GetBuildDetailsUrl(projectName, branchName, buildNumber), cancellationToken).ConfigureAwait(false);
        foreach (var p in xdoc.Descendants("artifact"))
        {
            var name = (string?)p.Element("relativePath");
            if (name != null)
                yield return name;
        }
    }
    public async Task DownloadArtifactAsync(string projectName, string? branchName, int buildNumber, string artifactName, Stream target, CancellationToken cancellationToken = default)
    {
        var url = GetBuildDetailsUrl(projectName, branchName, buildNumber);
        url = url[..^"api/xml".Length] + "artifact/" + artifactName;

        this.log?.LogDebug($"Downloading from {url}");
        var httpClient = await this.getHttpClientAsync().ConfigureAwait(false);
        using var stream = await httpClient.GetStreamAsync(url, cancellationToken).ConfigureAwait(false);
        await stream.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
    }
    public async Task<int> GetActualBuildNumber(string projectName, string? branchName, string buildNumberString, CancellationToken cancellationToken = default)
    {
        var myMaybeBuildNumber = AH.ParseInt(buildNumberString);
        if (myMaybeBuildNumber.HasValue)
            return myMaybeBuildNumber.Value;

        if (!SpecialBuildNumbers.Contains(buildNumberString))
            throw new ArgumentOutOfRangeException(nameof(buildNumberString), $"\"\" is not a numeric, or one of {(string.Join(", ", SpecialBuildNumbers))}");

        this.log?.LogDebug($"Looking up {buildNumberString} build...");

        var buildDoc = await this.GetXDocumentAsync(GetBuildDetailsUrl(projectName, branchName, buildNumberString), cancellationToken).ConfigureAwait(false);
        var nameElement = buildDoc.Root?.Element("number")
            ?? throw new InvalidOperationException("Could not find a <number> element under the root.");
        return AH.ParseInt(nameElement.Value)
            ?? throw new InvalidOperationException($"\"{nameElement.Value}\" is not an expected value for <number>");
    }

    public async Task<int> QueueBuildAsync(string projectName, string? branch, IEnumerable<KeyValuePair<string, string>>? parameters, CancellationToken cancellationToken = default)
    {
        var jobId = Uri.EscapeDataString(projectName);
        if (!string.IsNullOrEmpty(branch))
            jobId += $"/{Uri.EscapeDataString(branch)}";
        var url = $"job/{jobId}/build";
        if (parameters != null)
            url += "WithParameters";

        this.log?.LogDebug($"Queuing build to {url}");
        using var content = parameters == null ? null: new FormUrlEncodedContent(parameters);
        var httpClient = await this.getHttpClientAsync().ConfigureAwait(false);
        using var response = await httpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var location = response.Headers.Location?.OriginalString?.TrimEnd('/');
        var idx = location?.LastIndexOf('/')
            ?? throw new InvalidOperationException($"Unknown location header received: \"{location}\"");
        return AH.ParseInt(location[idx..].TrimStart('/'))
            ?? throw new InvalidOperationException($"Unexpected location header received: \"{location}\"");
    }
    public async Task<JenkinsQueuedBuildInfo> GetQueuedBuildInfoAsync(int queueItem, CancellationToken cancellationToken = default)
    {
        var url = "/queue/item/" + queueItem + "/api/xml";
        var item = (await this.GetXDocumentAsync(url, cancellationToken).ConfigureAwait(false)).Root
            ?? throw new InvalidOperationException("Unexpected null root element.");

        var buildNumber = item.Element("executable")?.Element("number")?.Value;
        if (!string.IsNullOrEmpty(buildNumber))
        {
            return new(int.Parse(buildNumber), null);
        }

        return new(null, item.Element("why")?.Value);
    }
    public async Task<JenkinsBuildInfo?> GetBuildInfoAsync(string projectName, string? branchName, int buildNumber, CancellationToken cancellationToken = default)
    {
        XDocument doc;
        try
        {
            var url = GetBuildDetailsUrl(projectName, branchName, buildNumber);
            doc = await this.GetXDocumentAsync(url, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException hex) when (hex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        var build = doc.Root ??
            throw new InvalidOperationException("Unexpected null root element.");

        return new 
        (
            (bool?)build.Element("building"),
            (string?)build.Element("number"),
            (string?)build.Element("result"),
            (int?)build.Element("duration"),
            (int?)build.Element("estimatedDuration")
        );
    }
    private async IAsyncEnumerable<CIBuildInfo> GetBuildsInternalAsync(string url, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var xdoc = await this.GetXDocumentAsync(url, cancellationToken).ConfigureAwait(false);
        if (xdoc.Root == null)
            yield break;

        if (xdoc.Root.Name.LocalName == "workflowMultiBranchProject")
        {
            foreach (var branchElement in xdoc.Root.Descendants("job"))
            {
                var name = (string?)branchElement.Element("name");
                if (string.IsNullOrEmpty(name))
                    continue;

                var branchUrl = (string?)branchElement.Element("url");
                if (string.IsNullOrEmpty(branchUrl))
                    continue;

                branchUrl = branchUrl.TrimEnd('/') + "/api/xml";

                await foreach (var b in this.GetBuildsInternalAsync(branchUrl, cancellationToken).ConfigureAwait(false))
                    yield return b with { Id = $"{name}-{b.Number}", Scope = name };
            }
        }
        else
        {
            foreach (var buildElement in xdoc.Descendants("build"))
            {
                var buildNumber = (string?)buildElement.Element("number");
                if (string.IsNullOrEmpty(buildNumber))
                    continue;

                var buildUrl = (string?)buildElement.Element("url");
                if (string.IsNullOrEmpty(buildUrl))
                    continue;

                var buildWebUrl = buildUrl;

                buildUrl = buildUrl.TrimEnd('/') + "/api/xml";

                var buildXdoc = await this.GetXDocumentAsync(buildUrl, cancellationToken).ConfigureAwait(false);
                var resultElement = buildXdoc.Descendants("result").FirstOrDefault();

                var timestampElement = buildXdoc.Descendants("timestamp").FirstOrDefault();
                if (timestampElement == null)
                    continue;

                long timestamp = (long)timestampElement;
                yield return new CIBuildInfo(buildNumber, string.Empty, buildNumber, DateTime.UnixEpoch + new TimeSpan(timestamp * TimeSpan.TicksPerMillisecond), (string?)resultElement ?? string.Empty, buildWebUrl);
            }
        }
    }
    private async Task<XDocument> GetXDocumentAsync(string url, CancellationToken cancellationToken)
    {
        this.log?.LogDebug($"Requesting XML from {url}");
        var httpClient = await this.getHttpClientAsync().ConfigureAwait(false);
        using var stream = await httpClient.GetStreamAsync(url, cancellationToken).ConfigureAwait(false);
        return await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken).ConfigureAwait(false);
    }
    private static string GetBuildDetailsUrl(string projectName, string? branchName, object buildNumber)
    {
        if (string.IsNullOrEmpty(branchName))
            return $"job/{Uri.EscapeDataString(projectName)}/{buildNumber}/api/xml";
        else
            return $"job/{Uri.EscapeDataString(projectName)}/job/{branchName}/{buildNumber}/api/xml";
    }
    public static void ParseBuildId(string buildId, out string? branchName, out int buildNumber)
    {
        int hyphenIndex = buildId.LastIndexOf('-');
        int? myMaybeBuildNumber;
        if (hyphenIndex < 0)
        {
            branchName = null;
            myMaybeBuildNumber = AH.ParseInt(buildId);
        }
        else
        {
            branchName = buildId[..hyphenIndex];
            myMaybeBuildNumber = AH.ParseInt(buildId[(hyphenIndex + 1)..]);
        }

        if (myMaybeBuildNumber == null)
            throw new FormatException($"{nameof(buildId)} has an unexpected format (\"{buildId}\").");
        
        buildNumber= myMaybeBuildNumber.Value;
    }
}

internal record JenkinsBuildInfo(bool? Building, string? Number, string? Result, int? Duration, int? EstimatedDuration);
internal record JenkinsQueuedBuildInfo(int? BuildNumber, string? WaitReason);