#load nuget:?package=Cake.Recipe&version=4.0.0

Environment.SetVariableNames();

BuildParameters.SetParameters(context: Context,
                              buildSystem: BuildSystem,
                              sourceDirectoryPath: "./src",
                              title: "Cake.Prompt",
                              repositoryOwner: "cake-contrib",
                              repositoryName: "Cake.Prompt",
                              appVeyorAccountName: "cakecontrib",
                              masterBranchName: "main",
                              shouldRunDotNetCorePack: true,
                              shouldBuildNugetSourcePackage: false,
                              shouldGenerateDocumentation: false,
                              shouldRunCodecov: false,
                              shouldRunInspectCode: false,
                              preferredBuildProviderType: BuildProviderType.GitHubActions,
                              integrationTestScriptPath: "./demo/script/prompt.cake");

BuildParameters.PrintParameters(Context);

ToolSettings.SetToolSettings(context: Context,
                             testCoverageFilter: "+[Cake.Prompt]* -[xunit.*]* -[Cake.Core]* -[Cake.Testing]* -[*.Tests]*",
                             testCoverageExcludeByAttribute: "*.ExcludeFromCodeCoverage*",
                             testCoverageExcludeByFile: "*/*Designer.cs;*/*.g.cs;*/*.g.i.cs");

// NOTE: no in-recipe Exercise-Script wiring here. Once an addin is on
// Cake.Core 3.0.0+ there's a chicken-and-egg version-mismatch —
// Cake.Recipe 4.0.0 pins cake.tool to a Cake-2-era runtime, so
// CakeExecuteScript inside the recipe can't load a Cake-3-targeted
// addin DLL (Cake.Common 3 removed DotNetCoreTestSettings; cake.tool
// 2.3.0 can't see Cake.Core 3.x). Instead the addin is exercised by
// the demo/{script,frosting} folders — each has its own cake.tool /
// Cake.Frosting pin matching the addin's Cake major and runs as a
// separate step in CI after the recipe's DotNetCore-Pack populates
// _PublishedLibraries.
//
// The xunit tests in src/Cake.Prompt.Tests still run in-recipe via
// the standard Test target — those don't depend on cake.tool's
// runtime, so coverage is preserved both ways.

Build.RunDotNetCore();
