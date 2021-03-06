﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Inedo.Extensions.Jenkins.Credentials;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using InedoExtensionTests;
using System.IO;

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
            { JobType.WorkflowMultiBranchProject, "BuildMaster multibranch" }
        };

        private string GetTestBranchName(JobType jobType)
        {
            if (jobType == JobType.WorkflowMultiBranchProject)
            {
                return MasterBranch;
            }

            return null;
        }

        private JenkinsClient CreateClient()
        {
            return new JenkinsClient(
                username: "admin",
                password: AH.CreateSecureString("test"),
                serverUrl: "http://localhost:8080",
                csrfProtectionEnabled: true
            );
        }

        [TestMethod()]
        public void GetJobNames()
        {
            using (var cts = new CancellationTokenSource(new TimeSpan(0, 0, 30)))
            {
                var client = CreateClient();
                var task = Task.Run<string[]>(async () => await client.GetJobNamesAsync().ConfigureAwait(false));
                var jobs = task.Result;
                
                Assert.IsTrue(jobs.Length > 1, "Expect more than one job to be defined in Jenkins");
                
                Assert.IsTrue(Array.IndexOf(jobs, JobNames[JobType.FreeStyleProject]) >= 0, $"FreeStyleProject ${JobNames[JobType.FreeStyleProject]} required");
                Assert.IsTrue(Array.IndexOf(jobs, JobNames[JobType.WorkflowJob]) >= 0, $"WorkflowProject ${JobNames[JobType.WorkflowJob]} required");
                Assert.IsTrue(Array.IndexOf(jobs, JobNames[JobType.WorkflowMultiBranchProject]) >= 0, $"WorkflowMultiBranchProject ${JobNames[JobType.WorkflowMultiBranchProject]} required");
            }
        }
        
        [TestMethod()]
        public void GetBranchNames()
        {
            using (var cts = new CancellationTokenSource(new TimeSpan(0, 0, 30)))
            {
                var client = CreateClient();
                var task = Task.Run<List<string>>(async () => await client.GetBranchNamesAsync(JobNames[JobType.WorkflowMultiBranchProject]).ConfigureAwait(false));
                var branches = task.Result;

                Assert.IsTrue(branches.Count > 1, $"Expect more than one branch to be defined for {JobType.WorkflowMultiBranchProject} job {JobNames[JobType.WorkflowMultiBranchProject]}");
                Assert.IsTrue(branches.Contains("master"), $"Expect master branch to be in the list for {JobType.WorkflowMultiBranchProject} job {JobNames[JobType.WorkflowMultiBranchProject]}");

                foreach (var branch in branches)
                {
                    Assert.IsTrue(branches.FindAll(b => b.Equals(branch)).Count == 1, $"Branch name {branch} should be unique for {JobType.WorkflowMultiBranchProject} job {JobNames[JobType.WorkflowMultiBranchProject]}");
                }
            }
        }
        
        [TestMethod()]
        public void GetBuildNumbers()
        {
            foreach (var job in JobNames)
            {
                using (var cts = new CancellationTokenSource(new TimeSpan(0, 0, 30)))
                {
                    var client = CreateClient();
                    var task = Task.Run<List<string>>(async () => await client.GetBuildNumbersAsync(job.Value, GetTestBranchName(job.Key)).ConfigureAwait(false));
                    var builds = task.Result;

                    Assert.IsTrue(builds.Count > 4, $"Expect more than one job to be defined for {job.Key} job {job.Value}");
                    Assert.AreEqual(builds[0], "lastSuccessfulBuild", $"Special build number values should be in list for {job.Key} job {job.Value}");
                    
                    foreach(var build in builds)
                    {
                        Assert.IsTrue(builds.FindAll(b => b.Equals(build)).Count == 1, $"Build number {build} should be unique for {job.Key} job {job.Value}");
                    }
                }
            }
        }

        [TestMethod()]
        [ExpectedException(typeof(System.InvalidOperationException), "An exception should be thrown for multi-branch projects if branch name not supplied")]
        public void GetBuildNumbers_MultiBranchJobThrowsExceptionWithoutBranchParameter()
        {
            using (var cts = new CancellationTokenSource(new TimeSpan(0, 0, 30)))
            {
                var client = CreateClient();
                var task = Task.Run<List<string>>(async () => await client.GetBuildNumbersAsync(JobNames[JobType.WorkflowMultiBranchProject], null).ConfigureAwait(false));
                task.GetAwaiter().GetResult();
            }
        }
        
        [TestMethod()]
        public void GetSpecialBuildNumber()
        {
            foreach (var job in JobNames)
            {
                using (var cts = new CancellationTokenSource(new TimeSpan(0, 0, 30)))
                {
                    var client = CreateClient();
                    var task = Task.Run<string>(async () => await client.GetSpecialBuildNumberAsync(job.Value, GetTestBranchName(job.Key), "lastSuccessfulBuild").ConfigureAwait(false));
                    var build = task.Result;

                    Assert.IsTrue(AH.ParseInt(build).HasValue, $"Special build number should be converted to actual build number for {job.Key} job {job.Value}");
                }
            }
        }

        [TestMethod()]
        public void GetSpecialBuildNumber_NoBuilds()
        {
            using (var cts = new CancellationTokenSource(new TimeSpan(0, 0, 30)))
            {
                var client = CreateClient();
                var task = Task.Run<string>(async () => await client.GetSpecialBuildNumberAsync(JobNames[JobType.FreeStyleProject], null, "invalidBuild").ConfigureAwait(false));
                var build = task.Result;
                
                Assert.IsTrue(build == null, "No build for special build number should return null");
            }
        }

        [TestMethod()]
        public void GetBuildInfo()
        {
            foreach (var job in JobNames)
            {
                using (var cts = new CancellationTokenSource(new TimeSpan(0, 0, 30)))
                {
                    var client = CreateClient();
                    var task = Task.Run<JenkinsBuild>(async () => await client.GetBuildInfoAsync(job.Value, GetTestBranchName(job.Key), "lastSuccessfulBuild").ConfigureAwait(false));
                    var build = task.Result;

                    Assert.IsFalse(build.Building, $"Build should be complete for {job.Key} job {job.Value}");
                }
            }
        }

        [TestMethod()]
        public void GetBuildArtifacts()
        {
            foreach (var job in JobNames)
            {
                if (job.Key == JobType.MatrixProject)
                    // Matrix not supported
                    continue;

                using (var cts = new CancellationTokenSource(new TimeSpan(0, 0, 30)))
                {
                    var client = CreateClient();
                    var task = Task.Run<List<JenkinsBuildArtifact>>(async () => await client.GetBuildArtifactsAsync(job.Value, GetTestBranchName(job.Key), "lastSuccessfulBuild").ConfigureAwait(false));
                    var artifacts = task.Result;

                    Assert.IsTrue(artifacts.Count > 0, $"Build should contain one or more artifacts for {job.Key} job {job.Value}");
                }
            }
        }

        [TestMethod()]
        public void DownloadArtifact()
        {
            foreach (var job in JobNames)
            {
                if (job.Key == JobType.MatrixProject)
                    // Matrix not supported
                    continue;

                using (var cts = new CancellationTokenSource(new TimeSpan(0, 0, 30)))
                using (var tmp = TempDir.Create())
                {
                    var zipFileName = tmp.GetPath("archive.zip");
                    Assert.IsFalse(File.Exists(zipFileName), $"Archive.zip should not exist prior to download for {job.Key} job {job.Value}");

                    var client = CreateClient();
                    var task = Task.Run(async () => await client.DownloadArtifactAsync(job.Value, GetTestBranchName(job.Key), "lastSuccessfulBuild", zipFileName).ConfigureAwait(false));
                    task.GetAwaiter().GetResult();

                    Assert.IsTrue(File.Exists(zipFileName), $"Archive.zip should be downloaded for {job.Key} job {job.Value}");
                }
            }
        }

        [TestMethod()]
        public void DownloadSingleArtifact()
        {
            foreach (var job in JobNames)
            {
                if (job.Key == JobType.MatrixProject)
                    // Matrix not supported
                    continue;

                using (var cts = new CancellationTokenSource(new TimeSpan(0, 0, 30)))
                using (var tmp = TempDir.Create())
                {
                    var zipFileName = tmp.GetPath("archive.zip");
                    Assert.IsFalse(File.Exists(zipFileName), $"Archive.zip should not exist prior to download for {job.Key} job {job.Value}");

                    var client = CreateClient();

                    var artifactTask = Task.Run<List<JenkinsBuildArtifact>>(async () => await client.GetBuildArtifactsAsync(job.Value, GetTestBranchName(job.Key), "lastSuccessfulBuild").ConfigureAwait(false));
                    var artifacts = artifactTask.Result;

                    var task = Task.Run(async () => await client.DownloadSingleArtifactAsync(job.Value, GetTestBranchName(job.Key), "lastSuccessfulBuild", zipFileName, artifacts[0]).ConfigureAwait(false));
                    task.GetAwaiter().GetResult();

                    Assert.IsTrue(File.Exists(zipFileName), $"Archive.zip should be downloaded for {job.Key} job {job.Value}");
                }
            }
        }


        [TestMethod()]
        public void OpenArtifact()
        {
            foreach (var job in JobNames)
            {
                if (job.Key == JobType.MatrixProject)
                    // Matrix not supported
                    continue;

                using (var cts = new CancellationTokenSource(new TimeSpan(0, 0, 30)))
                {
                    var client = CreateClient();
                    var task = Task.Run<OpenArtifact>(async () => await client.OpenArtifactAsync(job.Value, GetTestBranchName(job.Key), "lastSuccessfulBuild").ConfigureAwait(false));
                    var openArtifact = task.GetAwaiter().GetResult();

                    Assert.IsTrue(openArtifact.Content.Length > 0, $"Content should be downloaded for {job.Key} job {job.Value}");
                }
            }
        }

        [TestMethod()]
        public void OpenSingleArtifact()
        {
            foreach (var job in JobNames)
            {
                if (job.Key == JobType.MatrixProject)
                    // Matrix not supported
                    continue;

                using (var cts = new CancellationTokenSource(new TimeSpan(0, 0, 30)))
                {
                    var client = CreateClient();

                    var artifactTask = Task.Run<List<JenkinsBuildArtifact>>(async () => await client.GetBuildArtifactsAsync(job.Value, GetTestBranchName(job.Key), "lastSuccessfulBuild").ConfigureAwait(false));
                    var artifacts = artifactTask.Result;

                    var task = Task.Run<OpenArtifact>(async () => await client.OpenSingleArtifactAsync(job.Value, GetTestBranchName(job.Key), "lastSuccessfulBuild", artifacts[0]).ConfigureAwait(false));
                    var openArtifact = task.GetAwaiter().GetResult();

                    Assert.IsTrue(openArtifact.Content.Length > 0, $"Content should be downloaded for {job.Key} job {job.Value}");
                }
            }
        }

        [TestMethod()]
        public void TriggerBuild_AND_GetQueuedBuildInfo()
        {
            foreach (var job in JobNames)
            {
                using (var cts = new CancellationTokenSource(new TimeSpan(0, 0, 30)))
                {
                    var client = CreateClient();
                    
                    var triggerTask = Task.Run<int>(async () => await client.TriggerBuildAsync(job.Value, GetTestBranchName(job.Key), null).ConfigureAwait(false));
                    var queueId = triggerTask.GetAwaiter().GetResult();

                    Assert.IsTrue(queueId > 0, $"queueId should be greater than zero for {job.Key} job {job.Value}");

                    var queueTask = Task.Run<JenkinsQueueItem>(async () => await client.GetQueuedBuildInfoAsync(queueId));
                    var queueItem = queueTask.GetAwaiter().GetResult();

                    Assert.IsTrue(queueItem.BuildNumber == null, $"BuildNumber should be null for {job.Key} job {job.Value}");
                    Assert.IsTrue(queueItem.WaitReason.Contains("quiet period"), $"WaitReason should contain 'quiet period' for {job.Key} job {job.Value}");
                }
            }
        }
    }
}