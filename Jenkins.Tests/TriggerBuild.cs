using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MbUnit.Framework;

namespace Jenkins.Tests
{
    [TestFixture]
    public class TriggerBuild
    {
        const string BASEURL = "http://192.168.136.129/jenkins";

        Inedo.BuildMasterExtensions.Jenkins.TriggerBuildAction triggerBuildAction = new Inedo.BuildMasterExtensions.Jenkins.TriggerBuildAction();

        [SetUp]
        public void Init()
        {
            triggerBuildAction.TestConfigurer = new Inedo.BuildMasterExtensions.Jenkins.JenkinsConfigurer() { ServerUrl = BASEURL };
        }

        [Test]
        [Row("user","bitnami","HelloWorld",true)]
        [Row("user","f38904663e047480ff3168f2f0ef1ffd","HelloWorld", true)]
        [Row("", "bitnami", "HelloWorld", false)]
        [Row("user", "Bitnami", "HelloWorld", false)]
        public void RunBuild(string UserName, string Password, string JobName, bool Expected)
        {
            triggerBuildAction.TestConfigurer.Username = UserName;
            triggerBuildAction.TestConfigurer.Password = Password;
            triggerBuildAction.Job = JobName;
            Assert.AreEqual(Expected, triggerBuildAction.StartBuild());
        }

        [Test]
        [Row("user", "f38904663e047480ff3168f2f0ef1ffd", "HelloWorld", "nextBuildNumber")]
        public void GetJobField(string UserName, string Password, string JobName, string Field)
        {
            triggerBuildAction.TestConfigurer.Username = UserName;
            triggerBuildAction.TestConfigurer.Password = Password;
            triggerBuildAction.Job = JobName;
            Assert.IsNotEmpty(triggerBuildAction.GetJobField(Field));
        }

        [Test]
        [Row("user", "f38904663e047480ff3168f2f0ef1ffd", "HelloWorld")]
        public void GetLatestBuild(string UserName, string Password, string JobName)
        {
            triggerBuildAction.TestConfigurer.Username = UserName;
            triggerBuildAction.TestConfigurer.Password = Password;
            triggerBuildAction.Job = JobName;
            var latest = triggerBuildAction.LatestBuild();
            Assert.IsNotEmpty(latest.Number);
        }

        [Test]
        public void TestBuild()
        {
            triggerBuildAction.TestConfigurer.Username = "user";
            triggerBuildAction.TestConfigurer.Password = "f38904663e047480ff3168f2f0ef1ffd";
            triggerBuildAction.Job = "HelloWorld";
            triggerBuildAction.TestConfigurer.Delay = 10;
            triggerBuildAction.StartBuild();
            //System.Threading.Thread.Sleep(10000); // give Jenkins some time to create the build
            var latest = triggerBuildAction.LatestBuild();
            if (!latest.Building)
            {
                throw new Exception(string.Format("Build {0} is not building.", latest.Number));
            }
            triggerBuildAction.WaitForCompletion(latest);
        }

        [Test]
        [Row("user", "f38904663e047480ff3168f2f0ef1ffd", "HelloWorld", "lastBuild")]
        [Row("user", "f38904663e047480ff3168f2f0ef1ffd", "HelloWorld", "lastCompletedBuild")]
        [Row("user", "f38904663e047480ff3168f2f0ef1ffd", "HelloWorld", "lastFailedBuild")]
        [Row("user", "f38904663e047480ff3168f2f0ef1ffd", "HelloWorld", "lastStableBuild")]
        [Row("user", "f38904663e047480ff3168f2f0ef1ffd", "HelloWorld", "lastSuccessfulBuild")]
        [Row("user", "f38904663e047480ff3168f2f0ef1ffd", "HelloWorld", "lastUnsuccessfulBuild")]
        public void GetSpecialBuildNumber(string UserName, string Password, string JobName, string Special)
        {
            triggerBuildAction.TestConfigurer.Username = UserName;
            triggerBuildAction.TestConfigurer.Password = Password;
            triggerBuildAction.Job = JobName;
            Assert.IsNotEmpty(triggerBuildAction.GetSpecialBuildNumber(Special));
        }

        [Test]
        [Row("user", "f38904663e047480ff3168f2f0ef1ffd", "HelloWorld", "17")]
        public void ListArtifacts(string UserName, string Password, string JobName, string BuildNumber)
        {
            triggerBuildAction.TestConfigurer.Username = UserName;
            triggerBuildAction.TestConfigurer.Password = Password;
            triggerBuildAction.Job = JobName;
            Assert.AreNotEqual(0,triggerBuildAction.ListArtifacts(BuildNumber).Count());
        }

        [Test]
        public void TestGetArtifact()
        {
            triggerBuildAction.TestConfigurer.Username = "user";
            triggerBuildAction.TestConfigurer.Password = "f38904663e047480ff3168f2f0ef1ffd";
            triggerBuildAction.Job = "HelloWorld";
            triggerBuildAction.TestConfigurer.Delay = 10;
            
            
            int build = triggerBuildAction.GetBuildNumber("17");

            var artifacts = triggerBuildAction.ListArtifacts(build.ToString());
            string name = "HelloWorld.exe";
            if (artifacts.ContainsKey(name))
            {
                string fname = System.IO.Path.GetTempFileName();
                var result = triggerBuildAction.GetArtifact(17,artifacts[name], fname);
            }
        }

    }
}
