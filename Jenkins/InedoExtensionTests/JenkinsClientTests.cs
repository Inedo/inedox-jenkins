using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Inedo.Extensions.Jenkins.Credentials;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Inedo.Extensions.Jenkins.Tests
{
    /// <summary>
    /// These are not very good unit tests as they require a jenkins server with the correct user / token and job created, however 
    /// it does allow to quickly develop and confirm that Jenkins integration is working.
    /// 
    /// To get these tests working your Jenkins server needs the jobs listed below created and they must archive and artifact called Ezample.txt
    /// The multi-branch demo uses https://github.com/andrew-sumner/multibranch-demo to get it's jenkinsFile
    /// </summary>
    [TestClass()]
    public class JenkinsClientTests
    {
        private const string MasterBranch = "master";

        private enum JobType
        {
            FreeStyleProject, WorkflowJob, WorkflowMultiBranchProject, MatrixProject
        }

        private static readonly Dictionary<JobType, string> JobNames = new Dictionary<JobType, string>()
        {
            { JobType.FreeStyleProject, "demo-freestyle"},
            { JobType.WorkflowJob, "demo-pipeline" },
            { JobType.MatrixProject, "demo-multi-config" },
            { JobType.WorkflowMultiBranchProject, "demo-multibranch" }
        };

        public JenkinsCredentials ResourceCredentials => new JenkinsCredentials()
        {
            ServerUrl = "http://inedo:8080",
            UserName = "admin",
            Password = AH.CreateSecureString("11b54857bc1426530dc818fcbeb4f77d34")
        };

        [TestMethod()]
        public void GetJobNames()
        {
            using (var cts = new CancellationTokenSource(new TimeSpan(0, 0, 30)))
            {
                var client = new JenkinsClient(ResourceCredentials, null, cts.Token);

                var task = Task.Run<string[]>(async () => await client.GetJobNamesAsync().ConfigureAwait(false));
                var jobs = task.Result;
                
                Assert.IsTrue(jobs.Length > 1, "Expect more than one job to be defined in Jenkins");
                
                Assert.IsTrue(Array.IndexOf(jobs, JobNames[JobType.FreeStyleProject]) >= 0, $"FreeStyleProject ${JobNames[JobType.FreeStyleProject]} required");
                Assert.IsTrue(Array.IndexOf(jobs, JobNames[JobType.WorkflowJob]) >= 0, $"WorkflowProject ${JobNames[JobType.WorkflowJob]} required");
                Assert.IsTrue(Array.IndexOf(jobs, JobNames[JobType.WorkflowMultiBranchProject]) >= 0, $"WorkflowMultiBranchProject ${JobNames[JobType.WorkflowMultiBranchProject]} required");
            }
        }

        [TestMethod()]
        public void GetBuildNumbers()
        {
            foreach (var job in JobNames)
            {
                string branchName = null;

                if (job.Key == JobType.WorkflowMultiBranchProject)
                {
                    branchName = MasterBranch;
                }

                using (var cts = new CancellationTokenSource(new TimeSpan(0, 0, 30)))
                {
                    var client = new JenkinsClient(ResourceCredentials, null, cts.Token);

                    var task = Task.Run<List<string>>(async () => await client.GetBuildNumbersAsync(job.Value, branchName).ConfigureAwait(false));
                    var builds = task.Result;

                    Assert.IsTrue(builds.Count > 4, $"Expect more than one job to be defined for {job.Key} job {job.Value}");
                    Assert.AreEqual(builds[0], "lastSuccessfulBuild", $"Special build number values should be in list for {job.Key} job {job.Value}");
                }
            }
        }

        [TestMethod()]
        [ExpectedException(typeof(System.InvalidOperationException), "An exception should be thrown for multi-branch projects if branch name not supplied")]
        public void GetBuildNumbers_MultiBranchJobThrowsExceptionWithoutBranchParameter()
        {
            using (var cts = new CancellationTokenSource(new TimeSpan(0, 0, 30)))
            {
                var client = new JenkinsClient(ResourceCredentials, null, cts.Token);

                var task = Task.Run<List<string>>(async () => await client.GetBuildNumbersAsync(JobNames[JobType.WorkflowMultiBranchProject]).ConfigureAwait(false));
                task.WaitAndUnwrapExceptions();
            }
        }
        
        [TestMethod()]
        public void GetSpecialBuildNumber()
        {
            foreach (var job in JobNames)
            {
                using (var cts = new CancellationTokenSource(new TimeSpan(0, 0, 30)))
                {
                    string branchName = null;

                    if (job.Key == JobType.WorkflowMultiBranchProject)
                    {
                        branchName = MasterBranch;
                    }

                    var client = new JenkinsClient(ResourceCredentials, null, cts.Token);

                    var task = Task.Run<string>(async () => await client.GetSpecialBuildNumberAsync(job.Value, "lastSuccessfulBuild", branchName).ConfigureAwait(false));
                    var build = task.Result;

                    Assert.IsTrue(long.TryParse(build, out long n), $"Special build number should be converted to actual build number for {job.Key} job {job.Value}");
                }
            }
        }

        [TestMethod()]
        public void GetSpecialBuildNumber_NoBuilds()
        {
            using (var cts = new CancellationTokenSource(new TimeSpan(0, 0, 30)))
            {
                var client = new JenkinsClient(ResourceCredentials, null, cts.Token);

                var task = Task.Run<string>(async () => await client.GetSpecialBuildNumberAsync(JobNames[JobType.FreeStyleProject], "invalidBuild").ConfigureAwait(false));
                var build = task.Result;
                
                Assert.IsTrue(build == null, "No build for special build number should return null");
            }
        }

        [TestMethod()]
        public void GetBuildArtifacts()
        {
            foreach (var job in JobNames)
            {
                using (var cts = new CancellationTokenSource(new TimeSpan(0, 0, 30)))
                {
                    string branchName = null;

                    if (job.Key == JobType.WorkflowMultiBranchProject)
                    {
                        branchName = MasterBranch;
                    }

                    var client = new JenkinsClient(ResourceCredentials, null, cts.Token);

                    var task = Task.Run<List<JenkinsBuildArtifact>>(async () => await client.GetBuildArtifactsAsync(job.Value, "lastSuccessfulBuild", branchName).ConfigureAwait(false));
                    var artifacts = task.Result;

                    if (job.Key == JobType.MatrixProject)
                    {
                        // Matrix not supported
                        Assert.AreEqual(artifacts.Count, 0, $"Not currently supported for {job.Key} job {job.Value}");
                    }
                    else
                    {
                        Assert.IsTrue(artifacts.Count > 0, $"Build should contain one or more artifacts for {job.Key} job {job.Value}");
                    }                    
                }
            }
        }

        [TestMethod()]
        public void GetBuildInfo()
        {
            foreach (var job in JobNames)
            {
                using (var cts = new CancellationTokenSource(new TimeSpan(0, 0, 30)))
                {
                    string branchName = null;

                    if (job.Key == JobType.WorkflowMultiBranchProject)
                    {
                        branchName = MasterBranch;
                    }

                    var client = new JenkinsClient(ResourceCredentials, null, cts.Token);

                    var task = Task.Run<JenkinsBuild>(async () => await client.GetBuildInfoAsync(job.Value, "lastSuccessfulBuild", branchName).ConfigureAwait(false));
                    var build = task.Result;

                    Assert.IsFalse(build.Building, $"Build should be complete for {job.Key} job {job.Value}");
                }
            }
        }

        [TestMethod()]
        public void GetBranchNames()
        {
            using (var cts = new CancellationTokenSource(new TimeSpan(0, 0, 30)))
            {
                var client = new JenkinsClient(ResourceCredentials, null, cts.Token);

                var task = Task.Run<List<string>>(async () => await client.GetBranchNamesAsync(JobNames[JobType.WorkflowMultiBranchProject]).ConfigureAwait(false));
                var branches = task.Result;

                Assert.IsTrue(branches.Count > 1, $"Expect more than one branch to be defined for {JobType.WorkflowMultiBranchProject} job {JobNames[JobType.WorkflowMultiBranchProject]}");
                Assert.IsTrue(branches.Contains("master"), $"Expect master branch to be in the list for {JobType.WorkflowMultiBranchProject} job {JobNames[JobType.WorkflowMultiBranchProject]}");
            }
        }

        //TODO
        //DownloadArtifactAsync
        //DownloadSingleArtifactAsync
        //OpenArtifactAsync
        //OpenSingleArtifactAsync
        //TriggerBuildAsync
        //GetQueuedBuildInfoAsync

    }
}