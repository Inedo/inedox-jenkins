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
            }
        }

        [TestMethod()]
        public void GetBuildNumbers()
        {
            using (var cts = new CancellationTokenSource(new TimeSpan(0, 0, 30)))
            {
                var client = new JenkinsClient(ResourceCredentials, null, cts.Token);

                var task = Task.Run<List<string>>(async () => await client.GetBuildNumbersAsync("build-demo").ConfigureAwait(false));
                var builds = task.Result;

                Assert.IsTrue(builds.Count > 4, "Expect more than one job to be defined in Jenkins");
                Assert.AreEqual(builds[0], "lastSuccessfulBuild", "Jenkins special build number values should be in list");
            }
        }

        [TestMethod()]
        [ExpectedException(typeof(System.Exception), AllowDerivedTypes = true)]
        public void GetBuildNumbers_FromMultiBranchJob()
        {
            using (var cts = new CancellationTokenSource(new TimeSpan(0, 0, 30)))
            {
                var client = new JenkinsClient(ResourceCredentials, null, cts.Token);

                var task = Task.Run<List<string>>(async () => await client.GetBuildNumbersAsync("multibranch-demo").ConfigureAwait(false));
                var builds = task.Result;

                Assert.IsTrue(builds.Count == 4, "Expect only special build numbers to be returned");
            }
        }

        [TestMethod()]
        public void GetSpecialBuildNumber()
        {
            using (var cts = new CancellationTokenSource(new TimeSpan(0, 0, 30)))
            {
                var client = new JenkinsClient(ResourceCredentials, null, cts.Token);

                var task = Task.Run<string>(async () => await client.GetSpecialBuildNumberAsync("build-demo", "lastSuccessfulBuild").ConfigureAwait(false));
                var build = task.Result;

                Assert.IsTrue(long.TryParse(build, out long n), "Special build number should be converted to actual build number");
            }
        }

        [TestMethod()]
        public void GetBuildArtifacts()
        {
            using (var cts = new CancellationTokenSource(new TimeSpan(0, 0, 30)))
            {
                var client = new JenkinsClient(ResourceCredentials, null, cts.Token);

                var task = Task.Run<List<JenkinsBuildArtifact>>(async () => await client.GetBuildArtifactsAsync("build-demo", "lastSuccessfulBuild").ConfigureAwait(false));
                var artifacts = task.Result;

                Assert.IsTrue(artifacts.Count == 1, "Build should contain one artifact");
            }
        }

        [TestMethod()]
        public void GetBuildInfo()
        {
            using (var cts = new CancellationTokenSource(new TimeSpan(0, 0, 30)))
            {
                var client = new JenkinsClient(ResourceCredentials, null, cts.Token);

                var task = Task.Run<JenkinsBuild>(async () => await client.GetBuildInfoAsync("build-demo", "lastSuccessfulBuild").ConfigureAwait(false));
                var build = task.Result;

                Assert.IsFalse(build.Building, "Build should be complete");
            }
        }
        
    }
}