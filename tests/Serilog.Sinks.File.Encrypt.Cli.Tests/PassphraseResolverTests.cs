namespace Serilog.Sinks.File.Encrypt.Cli.Tests;

public class PassphraseResolverTests : IDisposable
{
    private readonly TestConsole _console = new();
    private readonly MockFileSystem _fileSystem = new();

    private PassphraseResolver GetSut() => new(_console, _fileSystem);

    public void Dispose()
    {
        _console.Dispose();
        Environment.SetEnvironmentVariable(IPassphraseResolver.DefaultEnvironmentVariable, null);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Resolve_FromFile_ReturnsFirstLine()
    {
        _fileSystem.AddFile("pass.txt", new MockFileData("secret-line\nsecond line"));

        string? result = GetSut().Resolve("pass.txt", passphraseEnv: null, confirm: false);

        result.ShouldBe("secret-line");
    }

    [Fact]
    public void Resolve_MissingFile_Throws()
    {
        Should
            .Throw<PassphraseResolutionException>(() =>
                GetSut().Resolve("nope.txt", passphraseEnv: null, confirm: false)
            )
            .Message.ShouldContain("does not exist");
    }

    [Fact]
    public void Resolve_EmptyFile_Throws()
    {
        _fileSystem.AddFile("empty.txt", new MockFileData(string.Empty));

        Should
            .Throw<PassphraseResolutionException>(() =>
                GetSut().Resolve("empty.txt", passphraseEnv: null, confirm: false)
            )
            .Message.ShouldContain("empty");
    }

    [Fact]
    public void Resolve_FromNamedEnvironmentVariable_ReturnsValue()
    {
        string name = $"CLI_TEST_PASS_{Guid.NewGuid():N}";
        try
        {
            Environment.SetEnvironmentVariable(name, "env-secret");

            string? result = GetSut().Resolve(passphraseFile: null, name, confirm: false);

            result.ShouldBe("env-secret");
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [Fact]
    public void Resolve_UnsetNamedEnvironmentVariable_Throws()
    {
        string name = $"CLI_TEST_PASS_{Guid.NewGuid():N}";

        Should
            .Throw<PassphraseResolutionException>(() =>
                GetSut().Resolve(passphraseFile: null, name, confirm: false)
            )
            .Message.ShouldContain(name);
    }

    [Fact]
    public void Resolve_FallbackEnvironmentVariable_IsUsedWhenNoSourceGiven()
    {
        Environment.SetEnvironmentVariable(
            IPassphraseResolver.DefaultEnvironmentVariable,
            "fallback-secret"
        );

        string? result = GetSut()
            .Resolve(passphraseFile: null, passphraseEnv: null, confirm: false);

        result.ShouldBe("fallback-secret");
    }

    [Fact]
    public void Resolve_NoSourceNonInteractive_ReturnsNull()
    {
        // TestConsole is non-interactive by default
        string? result = GetSut()
            .Resolve(passphraseFile: null, passphraseEnv: null, confirm: false);

        result.ShouldBeNull();
    }

    [Fact]
    public void Resolve_InteractivePrompt_ReturnsEnteredPassphrase()
    {
        _console.Interactive();
        _console.Input.PushTextWithEnter("prompted-secret");

        string? result = GetSut()
            .Resolve(passphraseFile: null, passphraseEnv: null, confirm: false);

        result.ShouldBe("prompted-secret");
    }

    [Fact]
    public void Resolve_InteractiveConfirmMatch_ReturnsPassphrase()
    {
        _console.Interactive();
        _console.Input.PushTextWithEnter("prompted-secret");
        _console.Input.PushTextWithEnter("prompted-secret");

        string? result = GetSut().Resolve(passphraseFile: null, passphraseEnv: null, confirm: true);

        result.ShouldBe("prompted-secret");
    }

    [Fact]
    public void Resolve_InteractiveConfirmMismatch_Throws()
    {
        _console.Interactive();
        _console.Input.PushTextWithEnter("first-entry");
        _console.Input.PushTextWithEnter("different-entry");

        Should
            .Throw<PassphraseResolutionException>(() =>
                GetSut().Resolve(passphraseFile: null, passphraseEnv: null, confirm: true)
            )
            .Message.ShouldContain("do not match");
    }

    [Fact]
    public void Resolve_FileTakesPrecedenceOverEnv()
    {
        _fileSystem.AddFile("pass.txt", new MockFileData("file-secret"));
        string name = $"CLI_TEST_PASS_{Guid.NewGuid():N}";
        try
        {
            Environment.SetEnvironmentVariable(name, "env-secret");

            string? result = GetSut().Resolve("pass.txt", name, confirm: false);

            result.ShouldBe("file-secret");
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }
}
