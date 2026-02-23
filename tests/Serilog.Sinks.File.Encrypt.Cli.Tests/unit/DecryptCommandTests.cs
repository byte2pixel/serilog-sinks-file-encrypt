using NSubstitute.ExceptionExtensions;
using Serilog.Sinks.File.Encrypt.Models;
using Spectre.Console;

namespace Serilog.Sinks.File.Encrypt.Cli.Tests.unit;

public class DecryptCommandTests : CommandTestBase
{
    private readonly IInputResolver _inputResolver = Substitute.For<IInputResolver>();
    private readonly IOutputResolver _outputResolver = Substitute.For<IOutputResolver>();
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
    public async Task GivenNoFilesToDecrypt_ExecuteAsync_ReturnsSuccessWithWarning()
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
        result.ShouldBe(0); // Success (no files to process)
        TestConsole.Output.ShouldContain("⚠ No files found matching the specified path or pattern");
    }

    [Fact]
    public async Task ExecuteAsync_OverwritesExistingFiles()
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
        TestConsole.Output.ShouldContain("✓ Success: 1");
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

    private DecryptCommand GetSut(IFileSystem? fileSystem = null)
    {
        return new DecryptCommand(
            TestConsole,
            fileSystem ?? FileSystem,
            _inputResolver,
            _outputResolver
        );
    }

    /// <summary>
    /// Helper method to create an encrypted log file using EncryptedStream
    /// </summary>
    private static byte[] CreateEncryptedLogFile(string logContent, string rsaPublicKey)
    {
        using MemoryStream memoryStream = new();
        using var rsa = RSA.Create();
        rsa.FromString(rsaPublicKey);
        EncryptionOptions options = new(rsa);

        using (LogWriter logWriter = new(memoryStream, options))
        {
            byte[] logBytes = Encoding.UTF8.GetBytes(logContent);
            logWriter.Write(logBytes, 0, logBytes.Length);
            logWriter.Flush();
        }

        return memoryStream.ToArray();
    }

    private void ConfigureFileResolver(params string[] encryptedFiles) =>
        _inputResolver.ResolveFiles(Arg.Any<string>()).Returns(encryptedFiles);
}
