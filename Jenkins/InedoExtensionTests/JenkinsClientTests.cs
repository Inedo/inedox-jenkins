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
    /// </summary>
    [TestClass()]
    public class JenkinsClientTests
    {
        private static readonly Dictionary<JobType, string> JobNames = new Dictionary<JobType, string>()
        {
            { JobType.FreeStyleProject, "build-demo"},
            { JobType.WorkflowProject, "test-pipeline" },
            { JobType.WorkflowMultiBranchProject, "multibranch-demo" }
        };

        private enum JobType {
            FreeStyleProject, WorkflowProject, WorkflowMultiBranchProject
        }

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
                Assert.IsTrue(Array.IndexOf(jobs, JobNames[JobType.WorkflowProject]) >= 0, $"WorkflowProject ${JobNames[JobType.WorkflowProject]} required");
                Assert.IsTrue(Array.IndexOf(jobs, JobNames[JobType.WorkflowMultiBranchProject]) >= 0, $"WorkflowMultiBranchProject ${JobNames[JobType.WorkflowMultiBranchProject]} required");
            }
        }

        [TestMethod()]
        public void GetBuildNumbers()
        {
            foreach (var job in JobNames)
            {
                if (job.Key == JobType.WorkflowMultiBranchProject)
                {
                    // handled differently to other jobs
                    continue;
                }

                using (var cts = new CancellationTokenSource(new TimeSpan(0, 0, 30)))
                {
                    var client = new JenkinsClient(ResourceCredentials, null, cts.Token);

                    var task = Task.Run<List<string>>(async () => await client.GetBuildNumbersAsync(job.Value).ConfigureAwait(false));
                    var builds = task.Result;

                    Assert.IsTrue(builds.Count > 4, $"Expect more than one job to be defined in Jenkins for {job.Key} job {job.Value}");
                    Assert.AreEqual(builds[0], "lastSuccessfulBuild", $"Jenkins special build number values should be in list for {job.Key} job {job.Value}");
                }
            }
        }

        [TestMethod()]
        [ExpectedException(typeof(System.Exception), AllowDerivedTypes = true)]
        public void GetBuildNumbers_FromMultiBranchJob()
        {
            using (var cts = new CancellationTokenSource(new TimeSpan(0, 0, 30)))
            {
                var client = new JenkinsClient(ResourceCredentials, null, cts.Token);

                var task = Task.Run<List<string>>(async () => await client.GetBuildNumbersAsync(JobNames[JobType.WorkflowMultiBranchProject]).ConfigureAwait(false));
                var builds = task.Result;

                Assert.IsTrue(builds.Count == 4, "Expect only special build numbers to be returned");
            }
        }

        [TestMethod()]
        public void GetSpecialBuildNumber()
        {
            foreach (var job in JobNames)
            {
                using (var cts = new CancellationTokenSource(new TimeSpan(0, 0, 30)))
                {
                    var client = new JenkinsClient(ResourceCredentials, null, cts.Token);

                    var task = Task.Run<string>(async () => await client.GetSpecialBuildNumberAsync(job.Value, "lastSuccessfulBuild").ConfigureAwait(false));
                    var build = task.Result;

                    Assert.IsTrue(long.TryParse(build, out long n), $"Special build number should be converted to actual build number for {job.Key} job {job.Value}");
                }
            }
        }

        [TestMethod()]
        public void GetBuildArtifacts()
        {
            foreach (var job in JobNames)
            {
                using (var cts = new CancellationTokenSource(new TimeSpan(0, 0, 30)))
                {
                    var client = new JenkinsClient(ResourceCredentials, null, cts.Token);

                    var task = Task.Run<List<JenkinsBuildArtifact>>(async () => await client.GetBuildArtifactsAsync(job.Value, "lastSuccessfulBuild").ConfigureAwait(false));
                    var artifacts = task.Result;

                    Assert.IsTrue(artifacts.Count == 1, $"Build should contain one artifact for {job.Key} job {job.Value}");
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
                    var client = new JenkinsClient(ResourceCredentials, null, cts.Token);

                    var task = Task.Run<JenkinsBuild>(async () => await client.GetBuildInfoAsync(job.Value, "lastSuccessfulBuild").ConfigureAwait(false));
                    var build = task.Result;

                    Assert.IsFalse(build.Building, $"Build should be complete for {job.Key} job {job.Value}");
                }
            }
        }

        [TestMethod()]
        public void GetBranchNames()
        {
            foreach (var job in JobNames)
            {
                using (var cts = new CancellationTokenSource(new TimeSpan(0, 0, 30)))
                {
                    var client = new JenkinsClient(ResourceCredentials, null, cts.Token);

                    var task = Task.Run<List<string>>(async () => await client.GetBranchNamesAsync(job.Value).ConfigureAwait(false));
                    var branches = task.Result;

                    Assert.IsTrue(branches.Count > 1, $"Expect more than one branch to be defined in Jenkins for {job.Key} job {job.Value}");
                }
            }
        }
    }
}