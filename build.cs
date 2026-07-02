#:sdk Cake.Sdk@6.2.0

const string Solution = "./serilog-sinks-file-encrypt.sln";
const string CoverageDir = "./.coverage";

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
        ctx.CleanDirectory("./.coverage");
    });

Task("Restore")
    .Does(ctx =>
    {
        ctx.DotNetRestore(Solution);
    });

Task("Lint")
    .Does(ctx =>
    {
        ctx.DotNetFormatStyle(Solution, new DotNetFormatSettings { VerifyNoChanges = true });
    });

Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore")
    .IsDependentOn("Lint")
    .Does(ctx =>
    {
        var settings = new DotNetBuildSettings
        {
            Configuration = configuration,
            Verbosity = DotNetVerbosity.Minimal,
            NoLogo = true,
            NoRestore = true,
            NoIncremental = ctx.HasArgument("rebuild"),
            MSBuildSettings = new DotNetMSBuildSettings().TreatAllWarningsAs(
                MSBuildTreatAllWarningsAs.Error
            ),
        };

        // Build only the shipped source and test projects. Example projects under
        // ./examples are intentionally excluded: they are not shipped, so their
        // dependencies' vulnerability advisories or warnings should not fail the build.
        foreach (
            FilePath project in ctx.GetFiles("./src/**/*.csproj")
                + ctx.GetFiles("./tests/**/*.csproj")
        )
        {
            ctx.DotNetBuild(project.FullPath, settings);
        }
    });

Task("Test")
    .IsDependentOn("Build")
    .Does(ctx =>
    {
        bool collectCoverage =
            Argument("collect-coverage", false) || BuildSystem.IsRunningOnGitHubActions;

        if (collectCoverage)
        {
            ctx.CleanDirectory(CoverageDir);
        }

        foreach (FilePath testProject in ctx.GetFiles("./tests/**/*.Tests.csproj"))
        {
            string projectName = testProject.GetFilenameWithoutExtension().ToString();
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
                string projectResultsDir = System.IO.Path.Join(CoverageDir, projectName);
                settings.Collectors = ["XPlat Code Coverage"];
                settings.ResultsDirectory = projectResultsDir;
                settings.Loggers = ["trx;LogFilePrefix=testResults"];
            }

            ctx.Information($"Running tests for {projectName}...");
            ctx.DotNetTest(testProject.FullPath, settings);
        }
    });

Task("Package")
    .IsDependentOn("Test")
    .Does(ctx =>
    {
        var settings = new DotNetPackSettings
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
        };

        // Pack only the shipped source projects; example projects are excluded (see Build).
        foreach (FilePath project in ctx.GetFiles("./src/**/*.csproj"))
        {
            ctx.DotNetPack(project.FullPath, settings);
        }
    });

Task("Publish-NuGet")
    .WithCriteria(_ => BuildSystem.IsRunningOnGitHubActions, "Not running on GitHub Actions")
    .IsDependentOn("Package")
    .Does(ctx =>
    {
        string? apiKey = Argument<string?>("nuget-key", null);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new CakeException("No NuGet API key was provided.");
        }

        // Publish to GitHub Packages
        foreach (FilePath? file in ctx.GetFiles("./.artifacts/*.nupkg"))
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

Teardown(_ =>
{
    Information("Shutting down .NET core SDK tooling...");
    DotNetBuildServerShutdown();
});

////////////////////////////////////////////////////////////////
// Execution

RunTarget(target);
