#:sdk Cake.Sdk@6.0.0

string solution = "./serilog-sinks-file-encrypt.sln";
string[] testProjects = new[]
{
    "./tests/Serilog.Sinks.File.Encrypt.Tests/Serilog.Sinks.File.Encrypt.Tests.csproj",
    "./tests/Serilog.Sinks.File.Encrypt.Cli.Tests/Serilog.Sinks.File.Encrypt.Cli.Tests.csproj",
};

////////////////////////////////////////////////////////////////
// Arguments

string target = Argument("target", "Default");
string configuration = Argument("configuration", "Release");

////////////////////////////////////////////////////////////////
// Tasks

Task("Clean")
    .Does(ctx =>
    {
        ctx.CleanDirectory("./.artifacts");
    });

Task("Lint")
    .Does(ctx =>
    {
        ctx.DotNetFormatStyle(solution, new DotNetFormatSettings { VerifyNoChanges = true });
    });

Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("Lint")
    .Does(ctx =>
    {
        ctx.DotNetBuild(
            solution,
            new DotNetBuildSettings
            {
                Configuration = configuration,
                Verbosity = DotNetVerbosity.Minimal,
                NoLogo = true,
                NoRestore = true,
                NoIncremental = ctx.HasArgument("rebuild"),
                MSBuildSettings = new DotNetMSBuildSettings().TreatAllWarningsAs(
                    MSBuildTreatAllWarningsAs.Error
                ),
            }
        );
    });

Task("Test")
    .IsDependentOn("Build")
    .Does(ctx =>
    {
        bool collectCoverage =
            Argument("collect-coverage", false) || BuildSystem.IsRunningOnGitHubActions;
        string coverageDir = "./.coverage";

        if (collectCoverage)
        {
            ctx.CleanDirectory(coverageDir);
        }

        foreach (string? testProject in testProjects)
        {
            string projectName = System.IO.Path.GetFileNameWithoutExtension(testProject);
            var settings = new DotNetTestSettings
            {
                Configuration = configuration,
                Verbosity = DotNetVerbosity.Minimal,
                NoLogo = true,
                NoRestore = true,
                NoBuild = true,
            };

            if (collectCoverage)
            {
                // Create a unique directory for each test project's results
                string projectResultsDir = System.IO.Path.Join(coverageDir, projectName);
                settings.Collectors = ["XPlat Code Coverage"];
                settings.ResultsDirectory = projectResultsDir;
                settings.Loggers = ["trx;LogFilePrefix=testResults"];
            }

            ctx.Information($"Running tests for {projectName}...");
            ctx.DotNetTest(testProject, settings);
        }
    });

Task("Package")
    .IsDependentOn("Test")
    .Does(ctx =>
    {
        ctx.DotNetPack(
            solution,
            new DotNetPackSettings
            {
                Configuration = configuration,
                Verbosity = DotNetVerbosity.Minimal,
                NoLogo = true,
                NoRestore = true,
                NoBuild = true,
                OutputDirectory = "./.artifacts",
                MSBuildSettings = new DotNetMSBuildSettings().TreatAllWarningsAs(
                    MSBuildTreatAllWarningsAs.Error
                ),
            }
        );
    });

Task("Publish-NuGet")
    .WithCriteria(ctx => BuildSystem.IsRunningOnGitHubActions, "Not running on GitHub Actions")
    .IsDependentOn("Package")
    .Does(ctx =>
    {
        string? apiKey = Argument<string?>("nuget-key", null);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new CakeException("No NuGet API key was provided.");
        }

        // Publish to GitHub Packages
        foreach (var file in ctx.GetFiles("./.artifacts/*.nupkg"))
        {
            ctx.Information("Publishing {0}...", file.GetFilename().FullPath);
            DotNetNuGetPush(
                file.FullPath,
                new DotNetNuGetPushSettings
                {
                    Source = "https://api.nuget.org/v3/index.json",
                    ApiKey = apiKey,
                }
            );
        }
    });

////////////////////////////////////////////////////////////////
// Targets

Task("Publish").IsDependentOn("Publish-NuGet");

Task("Default").IsDependentOn("Package");

////////////////////////////////////////////////////////////////
// Execution

RunTarget(target);
