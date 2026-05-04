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
                              integrationTestScriptPath: "./prompt.cake",
                              shouldRunIntegrationTests: true);

BuildParameters.PrintParameters(Context);

ToolSettings.SetToolSettings(context: Context,
                             testCoverageFilter: "+[Cake.Prompt]* -[xunit.*]* -[Cake.Core]* -[Cake.Testing]* -[*.Tests]*",
                             testCoverageExcludeByAttribute: "*.ExcludeFromCodeCoverage*",
                             testCoverageExcludeByFile: "*/*Designer.cs;*/*.g.cs;*/*.g.i.cs");

// Wire the Group 6 exercise script into the CI quality bar so PRs and
// fork builds catch regressions before merge. See workspace memory
// feedback_recipe_integration_test_wiring.md.
BuildParameters.Tasks.ContinuousIntegrationTask.IsDependentOn("Run-Integration-Tests");

Build.RunDotNetCore();
