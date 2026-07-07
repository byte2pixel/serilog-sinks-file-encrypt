using NSubstitute.ExceptionExtensions;
using NSubstitute.ReturnsExtensions;
using Serilog.Sinks.File.Encrypt.Models;
using Spectre.Console;

namespace Serilog.Sinks.File.Encrypt.Cli.Tests;

public class DecryptCommandTests : CommandTestBase
{
    private readonly IInputResolver _inputResolver = Substitute.For<IInputResolver>();
    private readonly IOutputResolver _outputResolver = Substitute.For<IOutputResolver>();
    private readonly IPassphraseResolver _passphraseResolver =
        Substitute.For<IPassphraseResolver>();
    private readonly string _publicKey;
    private readonly string _privateKey;
    private readonly string _privateKeyPath = Path.Join("keys", "private_key.xml");
    private readonly string _logsDir = Path.Join("logs");
    private string EncryptedFile => Path.Join(_logsDir, "app1.log");
    private const string LogContent1 = "2024-11-26 14:00:00 [INF] Log file 1\n";

    public DecryptCommandTests()
    {
        (_publicKey, _privateKey) = CryptographicUtils.GenerateRsaKeyPair(format: KeyFormat.Pem);

        FileSystem.AddFile(_privateKeyPath, new MockFileData(_privateKey));
        FileSystem.AddFile(
            EncryptedFile,
            new MockFileData(CreateEncryptedLogFile(LogContent1, _publicKey))
        );
        ConfigureFileResolver(EncryptedFile);

        // Default: derive a .decrypted.log path next to the input file, honouring any explicit output path
        _outputResolver
            .ResolveOutputPath(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
            .Returns(callInfo =>
            {
                string inputFile = callInfo.ArgAt<string>(0);
                string? outputPath = callInfo.ArgAt<string?>(2);
                if (!string.IsNullOrWhiteSpace(outputPath))
                {
                    return outputPath;
                }
                string dir = Path.GetDirectoryName(inputFile) ?? string.Empty;
                string name = Path.GetFileNameWithoutExtension(inputFile);
                string ext = Path.GetExtension(inputFile);
                return Path.Join(dir, $"{name}.decrypted{ext}");
            });
    }

    # region Happy Path Tests
    [Fact]
    public async Task ExecuteAsync_WithValidEncryptedFile_DecryptsSuccessfully()
    {
        // Arrange
        string decryptedFilePath = Path.Join("logs", "decrypted.log");
        string auditLogPath = Path.Join(Path.GetTempPath(), $"audit_{Guid.NewGuid()}.log");
        AddMockEncryptedFile("appLogWithKeyId.log", LogContent1, "key-id-123");
        ConfigureFileResolver("appLogWithKeyId.log");
        DecryptCommand command = GetSut();
        DecryptCommand.Settings settings = new()
        {
            InputPath = "appLogWithKeyId.log",
            KeyFile = _privateKeyPath,
            OutputPath = decryptedFilePath,
            AuditLogPath = auditLogPath,
            KeyId = "key-id-123",
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(0); // Success

        FileSystem.File.Exists(decryptedFilePath).ShouldBeTrue();

        string decryptedContent = await FileSystem.File.ReadAllTextAsync(
            decryptedFilePath,
            TestContext.Current.CancellationToken
        );
        decryptedContent.ShouldBe(LogContent1);
        bool auditLogExists = System.IO.File.Exists(auditLogPath);
        auditLogExists.ShouldBeTrue();

        TestConsole.Output.ShouldContain("✓ Decrypted:");
        TestConsole.Output.ShouldContain("Reading private key from:");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidEncryptedFileWithKeyId_DecryptsSuccessfully()
    {
        // Arrange
        string decryptedFilePath = Path.Join("logs", "decrypted.log");
        string auditLogPath = Path.Join(Path.GetTempPath(), $"audit_{Guid.NewGuid()}.log");
        DecryptCommand command = GetSut();
        DecryptCommand.Settings settings = new()
        {
            InputPath = EncryptedFile,
            KeyFile = _privateKeyPath,
            OutputPath = decryptedFilePath,
            AuditLogPath = auditLogPath,
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(0); // Success

        FileSystem.File.Exists(decryptedFilePath).ShouldBeTrue();

        string decryptedContent = await FileSystem.File.ReadAllTextAsync(
            decryptedFilePath,
            TestContext.Current.CancellationToken
        );
        decryptedContent.ShouldBe(LogContent1);
        bool auditLogExists = System.IO.File.Exists(auditLogPath);
        auditLogExists.ShouldBeTrue();

        TestConsole.Output.ShouldContain("✓ Decrypted:");
        TestConsole.Output.ShouldContain("Reading private key from:");
    }

    [Fact]
    public async Task GivenNoFilesToDecrypt_ExecuteAsync_ReturnsNoFilesMatchedWithWarning()
    {
        // Arrange
        string emptyDir = Path.Join("empty");
        FileSystem.AddDirectory(emptyDir);
        _inputResolver.ResolveFiles(Arg.Any<string>()).Returns(Enumerable.Empty<string>());

        DecryptCommand command = GetSut();
        DecryptCommand.Settings settings = new()
        {
            InputPath = emptyDir,
            KeyFile = _privateKeyPath,
            OutputPath = Path.Join(emptyDir, "output.log"),
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(ExitCodes.NoFilesMatched);
        TestConsole.Output.ShouldContain("⚠ No files found matching the specified path or pattern");
    }

    [Fact]
    public async Task ExecuteAsync_WithForce_OverwritesExistingFiles()
    {
        // Arrange
        const string OldContent = "Old content";

        string decryptedFile = Path.Join("logs", "app1.decrypted.log");

        FileSystem.AddFile(decryptedFile, new MockFileData(OldContent));

        DecryptCommand command = GetSut();
        DecryptCommand.Settings settings = new()
        {
            InputPath = EncryptedFile,
            KeyFile = _privateKeyPath,
            Force = true,
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(0);
        string content = await FileSystem.File.ReadAllTextAsync(
            decryptedFile,
            TestContext.Current.CancellationToken
        );
        content.ShouldBe(LogContent1); // Should be new content, not old
        TestConsole.Output.ShouldContain("will be overwritten");
        TestConsole.Output.ShouldContain("✓ Decrypted:");
    }

    [Fact]
    public async Task ExecuteAsync_WithoutForce_RefusesExistingOutputAndExitsUsageError()
    {
        // Arrange
        const string OldContent = "Old content";
        string decryptedFile = Path.Join("logs", "app1.decrypted.log");
        FileSystem.AddFile(decryptedFile, new MockFileData(OldContent));

        DecryptCommand command = GetSut();
        DecryptCommand.Settings settings = new()
        {
            InputPath = EncryptedFile,
            KeyFile = _privateKeyPath,
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert — refused, original content untouched
        result.ShouldBe(ExitCodes.UsageError);
        string content = await FileSystem.File.ReadAllTextAsync(
            decryptedFile,
            TestContext.Current.CancellationToken
        );
        content.ShouldBe(OldContent);
        TestConsole.Output.ShouldContain("already exists (use --force to overwrite)");
        TestConsole.Output.ShouldNotContain("✓ Decrypted:");
    }

    [Fact]
    public async Task ExecuteAsync_RefusedAndSuccessMixed_ContinuesAndExitsUsageError()
    {
        // Arrange — two inputs: one with an existing output (refused), one fresh (succeeds)
        AddMockEncryptedFile(Path.Join("logs", "app2.log"), LogContent1);
        string existingOutput = Path.Join("logs", "app1.decrypted.log");
        FileSystem.AddFile(existingOutput, new MockFileData("Old content"));
        ConfigureFileResolver(EncryptedFile, Path.Join("logs", "app2.log"));

        DecryptCommand command = GetSut();
        DecryptCommand.Settings settings = new()
        {
            InputPath = Path.Join("logs", "*.log"),
            KeyFile = _privateKeyPath,
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert — the fresh file decrypted, the refused one left intact, exit 2 overall
        result.ShouldBe(ExitCodes.UsageError);
        TestConsole.Output.ShouldContain("already exists (use --force to overwrite)");
        TestConsole.Output.ShouldContain("✓ Decrypted:");
        FileSystem.File.ReadAllText(Path.Join("logs", "app2.decrypted.log")).ShouldBe(LogContent1);
    }

    [Fact]
    public async Task ExecuteAsync_CreatesOutputDirectoryIfNotExists()
    {
        // Arrange
        string outputFile = Path.Join("new-dir", "output.log");
        DecryptCommand command = GetSut();
        DecryptCommand.Settings settings = new()
        {
            InputPath = EncryptedFile,
            KeyFile = _privateKeyPath,
            OutputPath = outputFile,
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(0); // Success
        FileSystem.Directory.Exists(Path.Join("new-dir")).ShouldBeTrue();
        FileSystem.File.Exists(outputFile).ShouldBeTrue();
    }

    #endregion

    #region Sad Path Tests
    [Fact]
    public async Task ExecuteAsync_WithMixedSuccessAndFailure_ReturnsPartialSuccess()
    {
        // Arrange - One valid file and one file that will cause IOException
        string lockedFile = Path.Join("logs", "locked.log");
        FileSystem.AddFile(
            lockedFile,
            new MockFileData(CreateEncryptedLogFile(LogContent1, _publicKey))
        );

        ConfigureFileResolver(EncryptedFile, lockedFile);
        // Mock the file system to throw IOException when trying to read the locked file
        IFileSystem mockFs = Substitute.For<IFileSystem>();
        mockFs.File.Exists(_privateKeyPath).Returns(true);
        mockFs.File.Exists(EncryptedFile).Returns(true);
        mockFs.File.Exists(lockedFile).Returns(true);
        mockFs
            .File.ReadAllTextAsync(_privateKeyPath, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(_privateKey));

        mockFs
            .Directory.GetFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>())
            .Returns([EncryptedFile, lockedFile]);

        mockFs.File.OpenRead(EncryptedFile).Returns(_ => FileSystem.File.OpenRead(EncryptedFile));
        mockFs
            .File.When(f => f.OpenRead(lockedFile))
            .Do(_ => throw new IOException("File is locked"));

        mockFs
            .File.Create(Arg.Is<string>(s => s.Contains("app1")))
            .Returns(x => FileSystem.File.Create((string)x[0]));
        mockFs.File.Exists(Arg.Is<string>(s => s.Contains(".decrypted"))).Returns(false);
        mockFs.Directory.Exists(Arg.Any<string>()).Returns(true);

        DecryptCommand command = GetSut(mockFs);
        DecryptCommand.Settings settings = new()
        {
            InputPath = Path.Join("logs", "*.log"),
            KeyFile = _privateKeyPath,
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(1); // Failure due to one failed file
        TestConsole.Output.ShouldContain("Found 2 file(s) to decrypt");
        TestConsole.Output.ShouldContain("Succeeded"); // summary table
        TestConsole.Output.ShouldContain("✗ Failed: 1");
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidPrivateKey_ReturnsErrorAndDisplaysMessage()
    {
        // Arrange
        string decryptedFilePath = Path.Join("logs", "decrypted.log");

        const string InvalidPrivateKey = "<RSAKeyValue><Invalid>data</Invalid></RSAKeyValue>";

        FileSystem.AddFile(_privateKeyPath, new MockFileData(InvalidPrivateKey));

        DecryptCommand command = GetSut();
        DecryptCommand.Settings settings = new()
        {
            KeyFile = _privateKeyPath,
            InputPath = EncryptedFile,
            OutputPath = decryptedFilePath,
            Strict = true,
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(1); // Error
        // Could be either FormatException or CryptographicException depending on the error
        bool hasError =
            TestConsole.Output.Contains("Invalid RSA key for key ID")
            || TestConsole.Output.Contains("Decryption failed:");
        hasError.ShouldBeTrue();
    }

    [Fact]
    public async Task GivenUnAuthorizedAccessException_WhenDecrypting_ThenErrorIsLoggedAndReturned()
    {
        // Arrange
        DecryptCommand.Settings settings = new()
        {
            InputPath = @"c:\my.log",
            KeyFile = @"c:\my.key",
        };
        FileSystemSub.File.Exists(settings.KeyFile).Returns(true);
        FileSystemSub
            .File.ReadAllTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnauthorizedAccessException("Simulated Auth error"));
        DecryptCommand command = GetSut(FileSystemSub);
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );
        result.ShouldBe(1); // Error
        TestConsole.Output.ShouldContain("✗ Access denied: Simulated Auth error");
    }

    #endregion

    #region Seal Status Tests

    [Fact]
    public async Task ExecuteAsync_SealedFile_ReportsAllSessionsSealed()
    {
        // Arrange
        string decryptedFilePath = Path.Join("logs", "decrypted.log");
        DecryptCommand command = GetSut();
        DecryptCommand.Settings settings = new()
        {
            InputPath = EncryptedFile,
            KeyFile = _privateKeyPath,
            OutputPath = decryptedFilePath,
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(0);
        TestConsole.Output.ShouldContain("✓ All 1 session(s) sealed and complete");
    }

    [Fact]
    public async Task ExecuteAsync_UnsealedFile_WarnsButSucceedsByDefault()
    {
        // Arrange: simulate a crash — writer never disposed, so no seal record
        string unsealedFile = Path.Join("logs", "crashed.log");
        FileSystem.AddFile(
            unsealedFile,
            new MockFileData(CreateUnsealedEncryptedLogFile(LogContent1, _publicKey))
        );
        ConfigureFileResolver(unsealedFile);

        DecryptCommand command = GetSut();
        DecryptCommand.Settings settings = new()
        {
            InputPath = unsealedFile,
            KeyFile = _privateKeyPath,
            OutputPath = Path.Join("logs", "decrypted.log"),
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert: crash tolerance — reported but not fatal
        result.ShouldBe(0);
        TestConsole.Output.ShouldContain("UNSEALED");
        string decryptedContent = await FileSystem.File.ReadAllTextAsync(
            Path.Join("logs", "decrypted.log"),
            TestContext.Current.CancellationToken
        );
        decryptedContent.ShouldBe(LogContent1);
    }

    [Fact]
    public async Task ExecuteAsync_UnsealedFile_RequireSealedWithoutStrict_ExitsNotSealed()
    {
        // Arrange
        string unsealedFile = Path.Join("logs", "crashed.log");
        FileSystem.AddFile(
            unsealedFile,
            new MockFileData(CreateUnsealedEncryptedLogFile(LogContent1, _publicKey))
        );
        ConfigureFileResolver(unsealedFile);

        DecryptCommand command = GetSut();
        DecryptCommand.Settings settings = new()
        {
            InputPath = unsealedFile,
            KeyFile = _privateKeyPath,
            OutputPath = Path.Join("logs", "decrypted.log"),
            RequireSealed = true,
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert: without --strict the file still decrypts and reports, but the run
        // signals the unmet integrity requirement via exit code 5
        result.ShouldBe(ExitCodes.NotSealed);
        TestConsole.Output.ShouldContain("UNSEALED");
        TestConsole.Output.ShouldContain("--require-sealed");
    }

    [Fact]
    public async Task ExecuteAsync_UnsealedFile_RequireSealedAndStrict_Fails()
    {
        // Arrange
        string unsealedFile = Path.Join("logs", "crashed.log");
        FileSystem.AddFile(
            unsealedFile,
            new MockFileData(CreateUnsealedEncryptedLogFile(LogContent1, _publicKey))
        );
        ConfigureFileResolver(unsealedFile);

        DecryptCommand command = GetSut();
        DecryptCommand.Settings settings = new()
        {
            InputPath = unsealedFile,
            KeyFile = _privateKeyPath,
            OutputPath = Path.Join("logs", "decrypted.log"),
            RequireSealed = true,
            Strict = true,
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(1);
        TestConsole.Output.ShouldContain("✗ Decryption failed:");
        TestConsole.Output.ShouldContain("✗ Failed: 1");
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void GivenEmptyInputPath_WhenValidating_ThenValidationFailsWithErrorMessage()
    {
        // Arrange
        DecryptCommand command = GetSut();
        DecryptCommand.Settings settings = new() { InputPath = "", KeyFile = _privateKeyPath };

        // Act
        ValidationResult result = command.Validate(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings
        );

        // Assert
        result.Successful.ShouldBeFalse();
        result.Message.ShouldNotBeNull().ShouldContain("Input path is required.");
    }

    [Fact]
    public void GivenNonExistentKeyFile_WhenValidating_ThenValidationFailsWithErrorMessage()
    {
        // Arrange
        DecryptCommand command = GetSut();
        DecryptCommand.Settings settings = new()
        {
            InputPath = EncryptedFile,
            KeyFile = Path.Join("keys", "nonexistent_key.xml"),
        };

        // Act
        ValidationResult result = command.Validate(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings
        );

        // Assert
        result.Successful.ShouldBeFalse();
        result.Message.ShouldNotBeNull().ShouldContain("Key file");
        result.Message.ShouldContain("nonexistent_key.xml' does not exist.");
    }

    [Fact]
    public void GivenInputPath_WhenValidDirectoryNoPattern_ThenValidationFails()
    {
        // Arrange
        DecryptCommand command = GetSut();
        DecryptCommand.Settings settings = new()
        {
            InputPath = _logsDir,
            KeyFile = _privateKeyPath,
        };

        // Act
        ValidationResult result = command.Validate(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings
        );

        // Assert
        result.Successful.ShouldBeFalse();
        result
            .Message.ShouldNotBeNull()
            .ShouldContain(
                "Input path cannot be a directory. Please specify a file or add a pattern"
            );
    }

    [Fact]
    public void GivenInputPath_WhenNonExistentDirectory_ThenValidationFailsWithErrorMessage()
    {
        // Arrange
        DecryptCommand command = GetSut();
        DecryptCommand.Settings settings = new()
        {
            InputPath = Path.Join("logs", "nonexistent_dir"),
            KeyFile = _privateKeyPath,
        };

        // Act
        ValidationResult result = command.Validate(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings
        );

        // Assert
        result.Successful.ShouldBeFalse();
        result.Message.ShouldNotBeNull().ShouldContain("Input path");
        result.Message.ShouldContain("is not a valid file or directory with pattern.");
    }

    [Fact]
    public void GivenValidInputs_WhenValidating_ThenValidationSucceeds()
    {
        // Arrange
        DecryptCommand command = GetSut();
        DecryptCommand.Settings settings = new()
        {
            InputPath = EncryptedFile,
            KeyFile = _privateKeyPath,
        };

        // Act
        ValidationResult result = command.Validate(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings
        );

        // Assert
        result.Successful.ShouldBeTrue();
    }

    #endregion

    [Fact]
    public async Task ExecuteAsync_WithWrongKey_ReturnsNothingDecryptedAndRemovesEmptyOutput()
    {
        // Arrange — file encrypted with a different key pair than the one we decrypt with
        (string otherPublicKey, _) = CryptographicUtils.GenerateRsaKeyPair(format: KeyFormat.Pem);
        string decryptedFilePath = Path.Join("logs", "decrypted.log");
        FileSystem.AddFile(
            "foreign.log",
            new MockFileData(CreateEncryptedLogFile(LogContent1, otherPublicKey))
        );
        ConfigureFileResolver("foreign.log");
        DecryptCommand command = GetSut();
        DecryptCommand.Settings settings = new()
        {
            InputPath = "foreign.log",
            KeyFile = _privateKeyPath,
            OutputPath = decryptedFilePath,
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(ExitCodes.NothingDecrypted);
        TestConsole.Output.ShouldContain("⚠ Nothing decrypted:");
        TestConsole.Output.ShouldNotContain("✓ Decrypted:");
        FileSystem.File.Exists(decryptedFilePath).ShouldBeFalse(); // empty output removed
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyInputFile_ReturnsNothingDecrypted()
    {
        // Arrange — a zero-byte input file yields no sessions and no messages
        FileSystem.AddFile("empty.log", new MockFileData(Array.Empty<byte>()));
        ConfigureFileResolver("empty.log");
        DecryptCommand command = GetSut();
        DecryptCommand.Settings settings = new()
        {
            InputPath = "empty.log",
            KeyFile = _privateKeyPath,
            OutputPath = Path.Join("logs", "empty-decrypted.log"),
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(ExitCodes.NothingDecrypted);
        TestConsole.Output.ShouldContain("⚠ Nothing decrypted:");
    }

    [Fact]
    public async Task ExecuteAsync_MixedZeroOutputAndSuccess_StillReturnsNothingDecrypted()
    {
        // Arrange — one good file, one empty file; runtime failure absent so 4 wins over 0
        FileSystem.AddFile("empty.log", new MockFileData(Array.Empty<byte>()));
        ConfigureFileResolver(EncryptedFile, "empty.log");
        DecryptCommand command = GetSut();
        DecryptCommand.Settings settings = new() { InputPath = "*.log", KeyFile = _privateKeyPath };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(ExitCodes.NothingDecrypted);
        TestConsole.Output.ShouldContain("✓ Decrypted:");
        TestConsole.Output.ShouldContain("⚠ Nothing decrypted:");
    }

    [Fact]
    public async Task ExecuteAsync_WithEncryptedKeyAndPassphrase_DecryptsSuccessfully()
    {
        // Arrange — full round trip with a passphrase-encrypted private key
        const string Passphrase = "round-trip-secret";
        (string publicKey, string encryptedPrivateKey) = CryptographicUtils.GenerateRsaKeyPair(
            2048,
            KeyFormat.Pem,
            Passphrase
        );
        string keyPath = Path.Join("keys", "encrypted_key.pem");
        FileSystem.AddFile(keyPath, new MockFileData(encryptedPrivateKey));
        FileSystem.AddFile(
            "enc.log",
            new MockFileData(CreateEncryptedLogFile(LogContent1, publicKey))
        );
        ConfigureFileResolver("enc.log");
        _passphraseResolver
            .Resolve(Arg.Any<string?>(), Arg.Any<string?>(), confirm: false)
            .Returns(Passphrase);

        string decryptedFilePath = Path.Join("logs", "enc-decrypted.log");
        DecryptCommand command = GetSut();
        DecryptCommand.Settings settings = new()
        {
            InputPath = "enc.log",
            KeyFile = keyPath,
            OutputPath = decryptedFilePath,
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(ExitCodes.Success);
        (
            await FileSystem.File.ReadAllTextAsync(
                decryptedFilePath,
                TestContext.Current.CancellationToken
            )
        ).ShouldBe(LogContent1);
    }

    [Fact]
    public async Task ExecuteAsync_EncryptedKeyNoPassphraseSource_ExitsUsageError()
    {
        // Arrange
        (_, string encryptedPrivateKey) = CryptographicUtils.GenerateRsaKeyPair(
            2048,
            KeyFormat.Pem,
            "some-passphrase"
        );
        string keyPath = Path.Join("keys", "encrypted_key.pem");
        FileSystem.AddFile(keyPath, new MockFileData(encryptedPrivateKey));
        _passphraseResolver
            .Resolve(Arg.Any<string?>(), Arg.Any<string?>(), confirm: false)
            .ReturnsNull();

        DecryptCommand command = GetSut();
        DecryptCommand.Settings settings = new() { InputPath = EncryptedFile, KeyFile = keyPath };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(ExitCodes.UsageError);
        TestConsole.Output.ShouldContain("passphrase-encrypted");
    }

    [Fact]
    public async Task ExecuteAsync_EncryptedKeyWrongPassphrase_ExitsRuntimeFailure()
    {
        // Arrange
        (_, string encryptedPrivateKey) = CryptographicUtils.GenerateRsaKeyPair(
            2048,
            KeyFormat.Pem,
            "right-passphrase"
        );
        string keyPath = Path.Join("keys", "encrypted_key.pem");
        FileSystem.AddFile(keyPath, new MockFileData(encryptedPrivateKey));
        _passphraseResolver
            .Resolve(Arg.Any<string?>(), Arg.Any<string?>(), confirm: false)
            .Returns("wrong-passphrase");

        DecryptCommand command = GetSut();
        DecryptCommand.Settings settings = new() { InputPath = EncryptedFile, KeyFile = keyPath };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert — clear failure, not silent success
        result.ShouldBe(ExitCodes.RuntimeFailure);
        TestConsole.Output.ShouldContain("✗ Decryption failed:");
    }

    [Fact]
    public void Validate_DefaultKeyMissingButXmlExists_HintsAtOldDefault()
    {
        // Arrange — no private_key.pem, but a legacy private_key.xml is present
        FileSystem.AddFile("private_key.xml", new MockFileData(_privateKey));
        DecryptCommand command = GetSut();
        DecryptCommand.Settings settings = new() { InputPath = EncryptedFile };

        // Act
        ValidationResult result = command.Validate(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings
        );

        // Assert
        result.Successful.ShouldBeFalse();
        result.Message.ShouldNotBeNull();
        result.Message.ShouldContain("private_key.xml");
        result.Message.ShouldContain("-k private_key.xml");
    }

    [Fact]
    public async Task ExecuteAsync_WithJson_WritesOnlyJsonToStdoutAndHumanTextToStderr()
    {
        // Arrange — capture the error channel in a second console
        using TestConsole stderrConsole = new();
        Writer.ErrorConsoleFactory = () => stderrConsole;
        string decryptedFilePath = Path.Join("logs", "decrypted.log");
        DecryptCommand command = GetSut();
        DecryptCommand.Settings settings = new()
        {
            InputPath = EncryptedFile,
            KeyFile = _privateKeyPath,
            OutputPath = decryptedFilePath,
            Json = true,
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert — stdout is pure JSON
        result.ShouldBe(ExitCodes.Success);
        string stdout = TestConsole.Output;
        stdout.TrimStart().ShouldStartWith("{");
        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(stdout);
        doc.RootElement.GetProperty("schemaVersion").GetInt32().ShouldBe(1);
        doc.RootElement.GetProperty("exitCode").GetInt32().ShouldBe(0);
        System.Text.Json.JsonElement files = doc.RootElement.GetProperty("files");
        files.GetArrayLength().ShouldBe(1);
        files[0].GetProperty("outcome").GetString().ShouldBe("Success");
        files[0].GetProperty("decryptedMessages").GetInt32().ShouldBe(1);
        files[0]
            .GetProperty("sessions")[0]
            .GetProperty("sealStatus")
            .GetString()
            .ShouldBe("Sealed");
        doc.RootElement.GetProperty("summary").GetProperty("succeeded").GetInt32().ShouldBe(1);

        // Human text went to the error channel
        stderrConsole.Output.ShouldContain("✓ Decrypted:");
    }

    [Fact]
    public async Task ExecuteAsync_WithJson_ZeroOutputFile_ReportsOutcomeAndExitCode()
    {
        // Arrange
        using TestConsole stderrConsole = new();
        Writer.ErrorConsoleFactory = () => stderrConsole;
        FileSystem.AddFile("empty.log", new MockFileData(Array.Empty<byte>()));
        ConfigureFileResolver("empty.log");
        DecryptCommand command = GetSut();
        DecryptCommand.Settings settings = new()
        {
            InputPath = "empty.log",
            KeyFile = _privateKeyPath,
            OutputPath = Path.Join("logs", "empty-decrypted.log"),
            Json = true,
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(ExitCodes.NothingDecrypted);
        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(
            TestConsole.Output
        );
        doc.RootElement.GetProperty("exitCode").GetInt32().ShouldBe(ExitCodes.NothingDecrypted);
        doc.RootElement.GetProperty("files")[0]
            .GetProperty("outcome")
            .GetString()
            .ShouldBe("NothingDecrypted");
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulDecrypt_ShowsSessionTable()
    {
        // Arrange
        TestConsole.Profile.Width = 200;
        string decryptedFilePath = Path.Join("logs", "decrypted.log");
        DecryptCommand command = GetSut();
        DecryptCommand.Settings settings = new()
        {
            InputPath = EncryptedFile,
            KeyFile = _privateKeyPath,
            OutputPath = decryptedFilePath,
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert — per-file session table and run summary table are rendered
        result.ShouldBe(ExitCodes.Success);
        TestConsole.Output.ShouldContain("Session");
        TestConsole.Output.ShouldContain("Seal");
        TestConsole.Output.ShouldContain("Sealed");
        TestConsole.Output.ShouldContain("Summary");
        TestConsole.Output.ShouldContain("Succeeded");
    }

    [Fact]
    public async Task ExecuteAsync_WithQuiet_SuppressesInfoButKeepsWarnings()
    {
        // Arrange
        string decryptedFilePath = Path.Join("logs", "decrypted.log");
        DecryptCommand command = GetSut();
        DecryptCommand.Settings settings = new()
        {
            InputPath = EncryptedFile,
            KeyFile = _privateKeyPath,
            OutputPath = decryptedFilePath,
            Quiet = true,
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(ExitCodes.Success);
        FileSystem.File.Exists(decryptedFilePath).ShouldBeTrue();
        TestConsole.Output.ShouldNotContain("Reading private key from:");
        TestConsole.Output.ShouldNotContain("✓ Decrypted:");
        TestConsole.Output.ShouldNotContain("Summary:");
    }

    [Fact]
    public async Task ExecuteAsync_WithQuiet_StillShowsNoFilesWarning()
    {
        // Arrange
        _inputResolver.ResolveFiles(Arg.Any<string>()).Returns(Enumerable.Empty<string>());
        DecryptCommand command = GetSut();
        DecryptCommand.Settings settings = new()
        {
            InputPath = "missing/*.log",
            KeyFile = _privateKeyPath,
            Quiet = true,
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(ExitCodes.NoFilesMatched);
        TestConsole.Output.ShouldContain("⚠ No files found matching the specified path or pattern");
    }

    [Fact]
    public async Task ExecuteAsync_WithVerbose_ShowsDecryptionDetail()
    {
        // Arrange
        TestConsole.Profile.Width = 260; // keep the long verbose line from wrapping
        string decryptedFilePath = Path.Join("logs", "decrypted.log");
        DecryptCommand command = GetSut();
        DecryptCommand.Settings settings = new()
        {
            InputPath = EncryptedFile,
            KeyFile = _privateKeyPath,
            OutputPath = decryptedFilePath,
            Verbose = true,
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(ExitCodes.Success);
        TestConsole.Output.ShouldContain("resync attempts:");
        TestConsole.Output.ShouldContain("sessions: 1");
    }

    [Fact]
    public async Task ExecuteAsync_PathWithMarkupCharacters_RendersLiterallyWithoutThrowing()
    {
        // Regression guard: '[' and ']' are legal in file names; every console write goes
        // through MarkupLineInterpolated, which escapes interpolation holes, so a path
        // like "weird[1].log" must render literally instead of crashing markup parsing.
        string weirdFile = Path.Join("logs", "weird[1].log");
        FileSystem.AddFile(
            weirdFile,
            new MockFileData(CreateEncryptedLogFile(LogContent1, _publicKey))
        );
        ConfigureFileResolver(weirdFile);
        DecryptCommand command = GetSut();
        DecryptCommand.Settings settings = new()
        {
            InputPath = weirdFile,
            KeyFile = _privateKeyPath,
            OutputPath = Path.Join("logs", "weird[1].decrypted.log"),
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(ExitCodes.Success);
        TestConsole.Output.ShouldContain("weird[1].log");
    }

    private DecryptCommand GetSut(IFileSystem? fileSystem = null)
    {
        return new DecryptCommand(
            Writer,
            fileSystem ?? FileSystem,
            _inputResolver,
            _outputResolver,
            _passphraseResolver,
            new DecryptReporter(Writer)
        );
    }

    private void AddMockEncryptedFile(string fileName, string logMessage, string keyId = "")
    {
        FileSystem.AddFile(
            fileName,
            new MockFileData(CreateEncryptedLogFile(logMessage, _publicKey, keyId))
        );
    }

    /// <summary>
    /// Helper method to create an encrypted log file using EncryptedStream
    /// </summary>
    private static byte[] CreateEncryptedLogFile(
        string logContent,
        string rsaPublicKey,
        string keyId = ""
    )
    {
        using MemoryStream memoryStream = new();
        using var rsa = RSA.Create();
        rsa.FromString(rsaPublicKey);
        EncryptionOptions options = new(rsa, keyId);

        using (LogWriter logWriter = new(memoryStream, options))
        {
            byte[] logBytes = Encoding.UTF8.GetBytes(logContent);
            logWriter.Write(logBytes, 0, logBytes.Length);
            logWriter.Flush();
        }

        return memoryStream.ToArray();
    }

    /// <summary>
    /// Creates an encrypted log file whose writer is deliberately never disposed,
    /// simulating a crash: frames are flushed but no end-of-log seal is written.
    /// </summary>
    private static byte[] CreateUnsealedEncryptedLogFile(
        string logContent,
        string rsaPublicKey,
        string keyId = ""
    )
    {
        using MemoryStream memoryStream = new();
        using var rsa = RSA.Create();
        rsa.FromString(rsaPublicKey);
        EncryptionOptions options = new(rsa, keyId);

        LogWriter logWriter = new(memoryStream, options);
        byte[] logBytes = Encoding.UTF8.GetBytes(logContent);
        logWriter.Write(logBytes, 0, logBytes.Length);
        logWriter.Flush();

        return memoryStream.ToArray();
    }

    private void ConfigureFileResolver(params string[] encryptedFiles) =>
        _inputResolver.ResolveFiles(Arg.Any<string>()).Returns(encryptedFiles);
}
