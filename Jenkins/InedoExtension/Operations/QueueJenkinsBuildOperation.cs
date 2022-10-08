using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Jenkins.Credentials;
using Inedo.Web;

namespace Inedo.Extensions.Jenkins.Operations;

[DisplayName("Queue Jenkins Build")]
[Description("Queues a build in Jenkins, optionally waiting for its completion.")]
[ScriptAlias("Queue-Build")]
[Tag("builds")]
[Tag("jenkins")]
public sealed class QueueJenkinsBuildOperation : JenkinsOperation
{
    private volatile OperationProgress progress = new ("");
    
    [ScriptAlias("From")]
    [DisplayName("Jenkins resource")]
    [DefaultValue("$CIProject")]
    [SuggestableValue(typeof(SecureResourceSuggestionProvider<JenkinsProject>))]
    public override string? ResourceName { get; set; }
    [ScriptAlias("Project"), ScriptAlias("Job", Obsolete = true)]
    [DisplayName("Project name")]
    [DefaultValue("$JenkinsProjectName($CIProject)")]
    [SuggestableValue(typeof(ProjectNameSuggestionProvider))]
    public override string? ProjectName { get; set; }
    [ScriptAlias("Branch")]
    [DisplayName("Branch name")]
    [DefaultValue("$JenkinsBranchName($CIBuild)")]
    [SuggestableValue(typeof(BranchNameSuggestionProvider))]
    [Description("The branch name is required for a Jenkins multi-branch project, otherwise should be left empty.")]
    public override string? BranchName { get; set; }
    [ScriptAlias("BuildNumber")]
    [DisplayName("Build number")]
    [DefaultValue("$JenkinsBuildNumber($CIBuild)")]
    [Description("The build number may be a specific build number, or a special value such as \"lastSuccessfulBuild\", \"lastStableBuild\", \"lastBuild\", or \"lastCompletedBuild\".")]
    [SuggestableValue(typeof(BuildNumberSuggestionProvider))]
    public string? BuildNumber { get; set; }
    [Category("Advanced")]
    [ScriptAlias("Parameters")]
    [DisplayName("Jenkins build parameters")]
    [PlaceholderText("e.g. %(MyVar:MyValue, MyVar2:MyValue2)")]
    public IReadOnlyDictionary<string, RuntimeValue>? Parameters { get; set; }


    [Category("Advanced")]
    [ScriptAlias("WaitForStart")]
    [DisplayName("Wait for start")]
    [Description("Ignored if wait for completion is true.")]
    [DefaultValue(true)]
    public bool WaitForStart { get; set; } = true;
    [Category("Advanced")]
    [ScriptAlias("WaitForCompletion")]
    [DisplayName("Wait for completion")]
    [DefaultValue(true)]
    public bool WaitForCompletion { get; set; } = true;
    [Output]
    [Category("Advanced")]
    [ScriptAlias("JenkinsBuildNumber")]
    [DisplayName("Actual build number (output)")]
    [PlaceholderText("e.g. $ActualBuildNumber")]
    [Description("When you specify a Build Number like \"lastBuild\", this will output the real Jenkins BuildNumber into a runtime variable.")]
    public string? JenkinsBuildNumber { get; set; }


    [Undisclosed]
    [ScriptAlias("ProxyRequest", Obsolete = true)]
    [DisplayName("Use server in context")]
    [Description("When selected, this will proxy the HTTP calls through the server in context instead of using the server Otter or BuildMaster is installed on. If the server in context is SSH-based, then an error will be raised.")]
    public bool ProxyRequest { get; set; }
    [Undisclosed]
    [ScriptAlias("AdditionalParameters", Obsolete = true)]
    public string? AdditionalParameters { get; set; }

    public void SetProgress(OperationProgress progress) => this.progress = progress;
    public override OperationProgress GetProgress() => this.progress;

    public async override Task ExecuteAsync(IOperationExecutionContext context)
    {
        if (this.ProxyRequest)
            throw new ExecutionFailureException($"The \"ProxyRequest\" parameter is no longer supported.");
        if (this.ProjectName == null)
            throw new ExecutionFailureException($"No Jenkins project was specified, and there is no CI build associated with this execution.");
        if (!this.TryCreateClient(context, out var client))
            throw new ExecutionFailureException($"Could not create a connection to Jenkins resource \"{AH.CoalesceString(this.ResourceName, this.ServerUrl)}\".");

        if (!string.IsNullOrEmpty(this.AdditionalParameters))
        {
            this.LogWarning($"The AdditionalParameters parameter ({this.AdditionalParameters}) is deprecated; use {Parameters} instead.");
            this.Parameters = this.AdditionalParameters.Split("&")
                .Select(s =>
                {
                    var p = s.Split("=", 2);
                    return (Key: p[0], Value: p.Length == 1 ? "" : p[1]);
                })
                .ToDictionary(i => Uri.UnescapeDataString(i.Key), i => new RuntimeValue(Uri.UnescapeDataString(i.Value)));
        }

        var queueItem = await client.QueueBuildAsync(
            this.ProjectName, 
            this.BranchName,
            this.Parameters?.Select(p => new KeyValuePair<string, string>(p.Key, p.Value.AsString() ?? string.Empty)), 
            context.CancellationToken);

        this.LogInformation($"Jenkins build has been queued as item {queueItem}.");
        if (!this.WaitForStart && !this.WaitForCompletion)
        {
            this.LogDebug("The operation is not configured to wait for the build to start.");
            return;
        }

        int actualBuildNumber; string? lastReason = null;
        while (true)
        {
            await Task.Delay(2 * 1000, context.CancellationToken).ConfigureAwait(false);
            
            var (buildNumber, waitReason) = await client.GetQueuedBuildInfoAsync(queueItem).ConfigureAwait(false);
            if (buildNumber != null)
            {
                actualBuildNumber = buildNumber.Value;
                this.JenkinsBuildNumber = actualBuildNumber.ToString();
                this.LogInformation($"Jenkins build number is {actualBuildNumber}.");
                break;
            }

            if (waitReason != null && !string.Equals(lastReason, waitReason))
            {
                this.LogDebug($"Waiting for build to start... ({waitReason})");
                lastReason = waitReason;
                this.SetProgress(new OperationProgress(null, waitReason));
            }
        }

        if (this.WaitForCompletion)
        {
            this.LogInformation($"Waiting for build {actualBuildNumber} to complete...");

            JenkinsBuildInfo? buildInfo;
            int attempts = 5;
            while (true)
            {
                await Task.Delay(2 * 1000, context.CancellationToken).ConfigureAwait(false);
                buildInfo = await client.GetBuildInfoAsync(this.ProjectName, this.BranchName, actualBuildNumber).ConfigureAwait(false);
                if (buildInfo == null)
                {
                    this.LogDebug("Build information was not returned.");
                    if (attempts > 0)
                    {
                        this.LogDebug($"Reloading build data ({attempts} attempts remaining)...");
                        attempts--;
                        continue;
                    }
                    break;
                }

                // reset retry counter
                attempts = 5;

                if (buildInfo.Building != true)
                {
                    this.LogDebug("Build has finished building.");
                    this.SetProgress(new OperationProgress(100));
                    break;
                }

                if (buildInfo.Duration != null && buildInfo.EstimatedDuration != null)
                {
                    var progress = buildInfo.Duration.Value * 100 / buildInfo.EstimatedDuration.Value;
                    this.SetProgress(new OperationProgress(Math.Min(progress, 99)));
                }
            }

            if (string.Equals("success", buildInfo?.Result, StringComparison.OrdinalIgnoreCase))
                this.LogDebug("Build status returned: success");
            else
                this.LogError("Build not not report success; result was: " + (buildInfo?.Result ?? "<not returned>"));
        }
        else
        {
            this.LogDebug("The operation is not configured to wait for build completion.");
        }
    }


    protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
    {
        string projectName = config[nameof(this.ProjectName)];
        string branchName = config[nameof(this.BranchName)];
        if (!string.IsNullOrEmpty(branchName))
            projectName += $" (Branch ${branchName}";

        return new ExtendedRichDescription(
            new RichDescription("Queue Jenkins Build"),
            new RichDescription("on project ", new Hilite(projectName))
        );
    }


}
