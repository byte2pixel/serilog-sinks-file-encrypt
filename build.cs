#:sdk Cake.Sdk@6.0.0

var solution = "./serilog-sinks-file-encrypt.sln";
var testProject = "./test/Serilog.Sinks.File.Encrypt.Tests/Serilog.Sinks.File.Encrypt.Tests.csproj";

////////////////////////////////////////////////////////////////
// Arguments

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

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
        var collectCoverage = Argument<bool>("collect-coverage", true);
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
            settings.Collectors = new[] { "XPlat Code Coverage" };
            settings.ResultsDirectory = "./.coverage";
            settings.Loggers = new[] { "trx" };
        }

        ctx.DotNetTest(testProject, settings);
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
        var apiKey = Argument<string?>("nuget-key", null);
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
