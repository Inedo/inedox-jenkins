using Inedo.BuildMaster;
using Inedo.BuildMaster.Data;
using Inedo.BuildMaster.Extensibility.Recipes;
using Inedo.BuildMaster.Web;
using Inedo.BuildMasterExtensions.Jenkins;

namespace Jenkins
{
    [RecipeProperties(
       "Deploy Jenkins Build",
       "An application that captures a build artifact from Jenkins and deploys through multiple environments",
       RecipeScopes.NewApplication)]
    [CustomEditor(typeof(DeployJenkinsBuildRecipeEditor))]
    public sealed class DeployJenkinsBuildRecipe : RecipeBase, IApplicationCreatingRecipe, IWorkflowCreatingRecipe
    {
        public string ApplicationGroup { get; set; }
        public string ApplicationName { get; set; }
        public int ApplicationId { get; set; }

        public string WorkflowName { get; set; }
        public int[] WorkflowSteps { get; set; }
        public int WorkflowId { get; set; }

        public string TargetDeploymentPath { get; set; }
        public string Job { get; set; }
        public string ArtifactName { get; set; }

        public override void Execute()
        {
            int deployableId = Util.Recipes.CreateDeployable(this.ApplicationId, this.ApplicationName);
            string deployableName = this.ApplicationName;
            int firstEnvironmentId = this.WorkflowSteps[0];

            int planId = Util.Recipes.CreatePlan(this.ApplicationId, deployableId, firstEnvironmentId,
                "Get Artifact from Jenkins",
                "Actions in this group will retrieve an artifact from Jenkins and create it as a BuildMaster artifact."
            );

            Util.Recipes.AddAction(planId, 1, new GetArtifactAction
            {
                ArtifactName = this.ArtifactName,
                Job = this.Job,
                BuildNumber = "lastSuccessfulBuild"
            });

            Util.Recipes.AddAction(planId, 1, Util.Recipes.Munging.MungeCoreExAction(
                "Inedo.BuildMaster.Extensibility.Actions.Artifacts.CreateArtifactAction", new
                {
                    ArtifactName = deployableName
                })
            );

            foreach (int environmentId in this.WorkflowSteps)
            {
                Util.Recipes.CreatePlan(this.ApplicationId, null, environmentId,
                    "Stop Application",
                    "Stop/shutdown/disable the application or application servers prior to deployment."
                );

                planId = Util.Recipes.CreatePlan(this.ApplicationId, deployableId, environmentId,
                    "Deploy " + deployableName,
                    "Deploy the artifacts created in the build actions, and then any configuration files needed."
                );

                Util.Recipes.AddAction(planId, 1, Util.Recipes.Munging.MungeCoreExAction(
                    "Inedo.BuildMaster.Extensibility.Actions.Artifacts.DeployArtifactAction", new
                    {
                        ArtifactName = deployableName,
                        OverriddenTargetDirectory = this.TargetDeploymentPath,
                        DoNotClearTargetDirectory = false
                    })
                );

                Util.Recipes.CreatePlan(this.ApplicationId, null, environmentId,
                    "Start Application",
                    "Start the application or application servers after deployment, and possibly run some post-startup automated testing."
                );
            }

            Util.Recipes.CreateSetupRelease(this.ApplicationId, Domains.ReleaseNumberSchemes.MajorMinor, this.WorkflowId, new[] { deployableId });
        }
    }
}
