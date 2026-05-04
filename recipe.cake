#load nuget:?package=Cake.Recipe&version=4.0.0

Environment.SetVariableNames();

BuildParameters.SetParameters(context: Context,
                              buildSystem: BuildSystem,
                              sourceDirectoryPath: "./src",
                              title: "Cake.Prompt",
                              repositoryOwner: "cake-contrib",
                              repositoryName: "Cake.Prompt",
                              appVeyorAccountName: "cakecontrib",
                              shouldRunDotNetCorePack: true,
                              shouldBuildNugetSourcePackage: false,
                              shouldGenerateDocumentation: false,
                              shouldRunCodecov: false,
                              shouldRunInspectCode: false,
                              preferredBuildProviderType: BuildProviderType.GitHubActions,
                              integrationTestScriptPath: "./prompt.cake");

BuildParameters.PrintParameters(Context);

ToolSettings.SetToolSettings(context: Context,
                             testCoverageFilter: "+[Cake.Prompt]* -[xunit.*]* -[Cake.Core]* -[Cake.Testing]* -[*.Tests]*",
                             testCoverageExcludeByAttribute: "*.ExcludeFromCodeCoverage*",
                             testCoverageExcludeByFile: "*/*Designer.cs;*/*.g.cs;*/*.g.i.cs");

// Wire the Group 6 exercise script into both local and CI builds.
// Cake.Recipe's built-in Run-Integration-Tests task IsDependentOn("Default"),
// so we can't make Default depend on it without creating a cycle. Instead
// define a separate Exercise-Script task that depends on Package (so the
// addin is built + packed first) and chain it from Default and CI so the
// script runs on every build, not just CI. See workspace memory
// feedback_recipe_integration_test_wiring.md.
Task("Exercise-Script")
    .IsDependentOn("Package")
    .Does(() =>
{
    CakeExecuteScript(BuildParameters.IntegrationTestScriptPath,
        new CakeSettings
        {
            Arguments = new Dictionary<string, string>
            {
                { "verbosity", Context.Log.Verbosity.ToString("F") }
            }
        });
});

BuildParameters.Tasks.DefaultTask.IsDependentOn("Exercise-Script");
BuildParameters.Tasks.ContinuousIntegrationTask.IsDependentOn("Exercise-Script");

Build.RunDotNetCore();
