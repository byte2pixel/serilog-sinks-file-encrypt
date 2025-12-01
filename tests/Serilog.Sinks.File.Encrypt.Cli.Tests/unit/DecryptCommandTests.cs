namespace Serilog.Sinks.File.Encrypt.Cli.Tests.unit;

public class DecryptCommandTests : CommandTestBase
{
    [Fact]
    public async Task ExecuteAsync_WithValidEncryptedFile_DecryptsSuccessfully()
    {
        // Arrange
        const string TestLogContent =
            "2024-11-26 14:00:00 [INF] Test log entry\n2024-11-26 14:00:01 [WRN] Warning message\n";
        string privateKeyPath = Path.Join("keys", "private_key.xml");
        string encryptedFilePath = Path.Join("logs", "encrypted.log");
        string decryptedFilePath = Path.Join("logs", "decrypted.log");

        (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair();

        byte[] encryptedContent = CreateEncryptedLogFile(TestLogContent, publicKey);

        FileSystem.AddFile(privateKeyPath, new MockFileData(privateKey));
        FileSystem.AddFile(encryptedFilePath, new MockFileData(encryptedContent));

        DecryptCommand command = new(TestConsole, FileSystem);
        DecryptCommand.Settings settings = new()
        {
            InputPath = encryptedFilePath,
            KeyFile = privateKeyPath,
            OutputPath = decryptedFilePath,
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
        decryptedContent.ShouldBe(TestLogContent);

        TestConsole.Output.ShouldContain("✓ Decrypted:");
        TestConsole.Output.ShouldContain("Reading private key from:");
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingKeyFile_ReturnsError()
    {
        // Arrange
        FileSystem.AddFile(Path.Join("logs", "encrypted.log"), new MockFileData("dummy"));

        DecryptCommand command = new(TestConsole, FileSystem);
        DecryptCommand.Settings settings = new()
        {
            KeyFile = Path.Join("keys", "missing_key.xml"),
            InputPath = Path.Join("logs", "encrypted.log"),
            OutputPath = Path.Join("logs", "decrypted.log"),
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(1); // Error
        TestConsole.Output.ShouldContain("Error: Key file");
        TestConsole.Output.ShouldContain("does not exist");
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingEncryptedFile_ReturnsError()
    {
        // Arrange
        FileSystem.AddFile(
            Path.Join("keys", "private_key.xml"),
            new MockFileData("<RSAKeyValue>test</RSAKeyValue>")
        );

        DecryptCommand command = new(TestConsole, FileSystem);
        DecryptCommand.Settings settings = new()
        {
            InputPath = Path.Join("logs", "missing.log"),
            KeyFile = Path.Join("keys", "private_key.xml"),
            OutputPath = Path.Join("logs", "decrypted.log"),
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(0); // No files found returns success
        TestConsole.Output.ShouldContain("No files found matching the specified path or pattern");
    }

    [Fact]
    public async Task ExecuteAsync_WithSkipErrorMode_DisplaysErrorModeConfiguration()
    {
        // Arrange
        const string TestLogContent = "2024-11-26 14:00:00 [INF] Test log entry\n";
        string privateKeyPath = Path.Join("keys", "private_key.xml");
        string encryptedFilePath = Path.Join("logs", "encrypted.log");
        string decryptedFilePath = Path.Join("logs", "decrypted.log");

        (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair();

        byte[] encryptedContent = CreateEncryptedLogFile(TestLogContent, publicKey);

        FileSystem.AddFile(privateKeyPath, new MockFileData(privateKey));
        FileSystem.AddFile(encryptedFilePath, new MockFileData(encryptedContent));

        DecryptCommand command = new(TestConsole, FileSystem);
        DecryptCommand.Settings settings = new()
        {
            KeyFile = privateKeyPath,
            InputPath = encryptedFilePath,
            OutputPath = decryptedFilePath,
            ErrorMode = ErrorHandlingMode.Skip,
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(0);
        TestConsole.Output.ShouldContain("Error handling mode:");
        TestConsole.Output.ShouldContain("Skip");
    }

    [Fact]
    public async Task ExecuteAsync_WithWriteToErrorLogMode_DisplaysErrorLogPath()
    {
        // Arrange
        const string TestLogContent = "2024-11-26 14:00:00 [INF] Test log entry\n";
        string privateKeyPath = Path.Join("keys", "private_key.xml");
        string encryptedFilePath = Path.Join("logs", "encrypted.log");
        string decryptedFilePath = Path.Join("logs", "decrypted.log");
        string errorLogPath = Path.Join("logs", "errors.log");

        (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair();

        byte[] encryptedContent = CreateEncryptedLogFile(TestLogContent, publicKey);

        FileSystem.AddFile(privateKeyPath, new MockFileData(privateKey));
        FileSystem.AddFile(encryptedFilePath, new MockFileData(encryptedContent));

        DecryptCommand command = new(TestConsole, FileSystem);
        DecryptCommand.Settings settings = new()
        {
            KeyFile = privateKeyPath,
            InputPath = encryptedFilePath,
            OutputPath = decryptedFilePath,
            ErrorMode = ErrorHandlingMode.WriteToErrorLog,
            ErrorLogPath = errorLogPath,
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(0);
        TestConsole.Output.ShouldContain("Error handling mode:");
        TestConsole.Output.ShouldContain("WriteToErrorLog");
        TestConsole.Output.ShouldContain("Error log path:");
        TestConsole.Output.ShouldContain(errorLogPath);
    }

    [Fact]
    public async Task ExecuteAsync_WithThrowExceptionMode_DisplaysErrorModeConfiguration()
    {
        // Arrange
        const string TestLogContent = "2024-11-26 14:00:00 [INF] Test log entry\n";
        string privateKeyPath = Path.Join("keys", "private_key.xml");
        string encryptedFilePath = Path.Join("logs", "encrypted.log");
        string decryptedFilePath = Path.Join("logs", "decrypted.log");

        (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair();

        byte[] encryptedContent = CreateEncryptedLogFile(TestLogContent, publicKey);

        FileSystem.AddFile(privateKeyPath, new MockFileData(privateKey));
        FileSystem.AddFile(encryptedFilePath, new MockFileData(encryptedContent));

        DecryptCommand command = new(TestConsole, FileSystem);
        DecryptCommand.Settings settings = new()
        {
            InputPath = encryptedFilePath,
            KeyFile = privateKeyPath,
            OutputPath = decryptedFilePath,
            ErrorMode = ErrorHandlingMode.ThrowException,
            ContinueOnError = false,
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(0);
        TestConsole.Output.ShouldContain("Error handling mode:");
        TestConsole.Output.ShouldContain("ThrowException");
    }

    [Fact]
    public async Task ExecuteAsync_WhenReadingKeyFileFails_ReturnsErrorAndDisplaysMessage()
    {
        // Arrange
        const string TestLogContent = "2024-11-26 14:00:00 [INF] Test log entry\n";
        string privateKeyPath = Path.Join("keys", "private_key.xml");
        string encryptedFilePath = Path.Join("logs", "encrypted.log");
        string decryptedFilePath = Path.Join("logs", "decrypted.log");

        (string publicKey, string _) = EncryptionUtils.GenerateRsaKeyPair();
        byte[] encryptedContent = CreateEncryptedLogFile(TestLogContent, publicKey);

        // Create properly encrypted file
        FileSystem.AddFile(encryptedFilePath, new MockFileData(encryptedContent));

        // Use NSubstitute to create a mock that throws IOException when reading key file
        IFileSystem mockFs = Substitute.For<IFileSystem>();
        mockFs.File.Exists(privateKeyPath).Returns(true);
        mockFs.File.Exists(encryptedFilePath).Returns(true);
        mockFs
            .File.ReadAllTextAsync(privateKeyPath, Arg.Any<CancellationToken>())
            .Returns<string>(_ => throw new IOException("Unable to read key file"));

        DecryptCommand command = new(TestConsole, mockFs);
        DecryptCommand.Settings settings = new()
        {
            KeyFile = privateKeyPath,
            InputPath = encryptedFilePath,
            OutputPath = decryptedFilePath,
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(1); // Error
        TestConsole.Output.ShouldContain("Error reading or writing files:");
        TestConsole.Output.ShouldContain("Unable to read key file");
    }

    [Fact]
    public async Task ExecuteAsync_WhenOpeningEncryptedFileFails_ReturnsErrorAndDisplaysMessage()
    {
        // Arrange
        const string TestLogContent = "2024-11-26 14:00:00 [INF] Test log entry\n";
        string privateKeyPath = Path.Join("keys", "private_key.xml");
        string encryptedFilePath = Path.Join("logs", "encrypted.log");
        string decryptedFilePath = Path.Join("logs", "decrypted.log");

        (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair();
        byte[] encryptedContent = CreateEncryptedLogFile(TestLogContent, publicKey);

        // Add files to MockFileSystem
        FileSystem.AddFile(privateKeyPath, new MockFileData(privateKey));
        FileSystem.AddFile(encryptedFilePath, new MockFileData(encryptedContent));

        // Use NSubstitute to create a mock that throws IOException when opening encrypted file
        IFileSystem mockFs = Substitute.For<IFileSystem>();
        mockFs.File.Exists(privateKeyPath).Returns(true);
        mockFs.File.Exists(encryptedFilePath).Returns(true);
        mockFs
            .File.ReadAllTextAsync(privateKeyPath, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(privateKey));

        mockFs
            .File.When(f => f.OpenRead(encryptedFilePath))
            .Do(_ => throw new IOException("File is locked by another process"));

        DecryptCommand command = new(TestConsole, mockFs);
        DecryptCommand.Settings settings = new()
        {
            KeyFile = privateKeyPath,
            InputPath = encryptedFilePath,
            OutputPath = decryptedFilePath,
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(1); // Error - failures in processing
        TestConsole.Output.ShouldContain("✗ Failed:");
        TestConsole.Output.ShouldContain("File is locked by another process");
    }

    [Fact]
    public async Task ExecuteAsync_WhenCreatingOutputFileFails_ReturnsErrorAndDisplaysMessage()
    {
        // Arrange
        const string TestLogContent = "2024-11-26 14:00:00 [INF] Test log entry\n";
        string privateKeyPath = Path.Join("keys", "private_key.xml");
        string encryptedFilePath = Path.Join("logs", "encrypted.log");
        string decryptedFilePath = Path.Join("logs", "decrypted.log");

        (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair();

        byte[] encryptedContent = CreateEncryptedLogFile(TestLogContent, publicKey);

        FileSystem.AddFile(privateKeyPath, new MockFileData(privateKey));
        FileSystem.AddFile(encryptedFilePath, new MockFileData(encryptedContent));

        // Use NSubstitute to create a mock that throws UnauthorizedAccessException when creating output file
        IFileSystem mockFs = Substitute.For<IFileSystem>();
        mockFs.File.Exists(privateKeyPath).Returns(true);
        mockFs.File.Exists(encryptedFilePath).Returns(true);
        mockFs
            .File.ReadAllTextAsync(privateKeyPath, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(privateKey));

        mockFs
            .File.OpenRead(encryptedFilePath)
            .Returns(_ => FileSystem.File.OpenRead(encryptedFilePath));

        mockFs
            .File.When(f => f.Create(decryptedFilePath))
            .Do(_ => throw new UnauthorizedAccessException("Access denied to output directory"));

        DecryptCommand command = new(TestConsole, mockFs);
        DecryptCommand.Settings settings = new()
        {
            KeyFile = privateKeyPath,
            InputPath = encryptedFilePath,
            OutputPath = decryptedFilePath,
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(1); // Error - failures in processing
        TestConsole.Output.ShouldContain("✗ Failed:");
        TestConsole.Output.ShouldContain("Access denied to output directory");
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidPrivateKey_ReturnsErrorAndDisplaysMessage()
    {
        // Arrange
        const string TestLogContent = "2024-11-26 14:00:00 [INF] Test log entry\n";
        string privateKeyPath = Path.Join("keys", "private_key.xml");
        string encryptedFilePath = Path.Join("logs", "encrypted.log");
        string decryptedFilePath = Path.Join("logs", "decrypted.log");

        (string publicKey, string _) = EncryptionUtils.GenerateRsaKeyPair();
        string invalidPrivateKey = "<RSAKeyValue><Invalid>data</Invalid></RSAKeyValue>";

        byte[] encryptedContent = CreateEncryptedLogFile(TestLogContent, publicKey);

        FileSystem.AddFile(privateKeyPath, new MockFileData(invalidPrivateKey));
        FileSystem.AddFile(encryptedFilePath, new MockFileData(encryptedContent));

        DecryptCommand command = new(TestConsole, FileSystem);
        DecryptCommand.Settings settings = new()
        {
            KeyFile = privateKeyPath,
            InputPath = encryptedFilePath,
            OutputPath = decryptedFilePath,
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
            TestConsole.Output.Contains("Invalid key or file format:")
            || TestConsole.Output.Contains("Decryption failed:");
        hasError.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithWrongPrivateKey_ReturnsErrorAndDisplaysMessage()
    {
        // Arrange
        const string TestLogContent = "2024-11-26 14:00:00 [INF] Test log entry\n";
        string privateKeyPath = Path.Join("keys", "private_key.xml");
        string encryptedFilePath = Path.Join("logs", "encrypted.log");
        string decryptedFilePath = Path.Join("logs", "decrypted.log");

        // Generate two different key pairs
        (string publicKey, string _) = EncryptionUtils.GenerateRsaKeyPair();
        (string _, string wrongPrivateKey) = EncryptionUtils.GenerateRsaKeyPair();

        byte[] encryptedContent = CreateEncryptedLogFile(TestLogContent, publicKey);

        FileSystem.AddFile(privateKeyPath, new MockFileData(wrongPrivateKey));
        FileSystem.AddFile(encryptedFilePath, new MockFileData(encryptedContent));

        DecryptCommand command = new(TestConsole, FileSystem);
        DecryptCommand.Settings settings = new()
        {
            KeyFile = privateKeyPath,
            InputPath = encryptedFilePath,
            OutputPath = decryptedFilePath,
            ContinueOnError = false,
            ErrorMode = ErrorHandlingMode.ThrowException,
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(1); // Error
        TestConsole.Output.ShouldContain("Decryption failed:");
    }

    [Fact]
    public async Task ExecuteAsync_WithCorruptedEncryptedFile_ReturnsErrorAndDisplaysMessage()
    {
        // Arrange
        string privateKeyPath = Path.Join("keys", "private_key.xml");
        string encryptedFilePath = Path.Join("logs", "encrypted.log");
        string decryptedFilePath = Path.Join("logs", "decrypted.log");

        (string _, string privateKey) = EncryptionUtils.GenerateRsaKeyPair();

        // Create a corrupted encrypted file (just random bytes that won't decrypt properly)
        byte[] corruptedContent = [0x00, 0x01, 0x02, 0x03, 0xFF, 0xFE, 0xFD];

        FileSystem.AddFile(privateKeyPath, new MockFileData(privateKey));
        FileSystem.AddFile(encryptedFilePath, new MockFileData(corruptedContent));

        DecryptCommand command = new(TestConsole, FileSystem);
        DecryptCommand.Settings settings = new()
        {
            InputPath = encryptedFilePath,
            KeyFile = privateKeyPath,
            OutputPath = decryptedFilePath,
            ContinueOnError = false,
            ErrorMode = ErrorHandlingMode.ThrowException,
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(1); // Error
        // Should detect that the file has no valid encryption markers
        // With ThrowException mode, the InvalidOperationException bubbles up to the top-level handler
        TestConsole.Output.ShouldContain("✗ Invalid file:");
        TestConsole.Output.ShouldContain("does not contain valid encryption markers");
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleFiles_DecryptsAllSuccessfully()
    {
        // Arrange
        const string LogContent1 = "2024-11-26 14:00:00 [INF] Log file 1\n";
        const string LogContent2 = "2024-11-26 14:00:01 [INF] Log file 2\n";
        const string LogContent3 = "2024-11-26 14:00:02 [INF] Log file 3\n";

        string privateKeyPath = Path.Join("keys", "private_key.xml");
        string encryptedFile1 = Path.Join("logs", "app1.log");
        string encryptedFile2 = Path.Join("logs", "app2.log");
        string encryptedFile3 = Path.Join("logs", "app3.log");
        string outputDir = Path.Join("decrypted");

        (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair();

        FileSystem.AddFile(privateKeyPath, new MockFileData(privateKey));
        FileSystem.AddFile(
            encryptedFile1,
            new MockFileData(CreateEncryptedLogFile(LogContent1, publicKey))
        );
        FileSystem.AddFile(
            encryptedFile2,
            new MockFileData(CreateEncryptedLogFile(LogContent2, publicKey))
        );
        FileSystem.AddFile(
            encryptedFile3,
            new MockFileData(CreateEncryptedLogFile(LogContent3, publicKey))
        );
        FileSystem.AddDirectory(outputDir);

        DecryptCommand command = new(TestConsole, FileSystem);
        DecryptCommand.Settings settings = new()
        {
            InputPath = Path.Join("logs", "*.log"),
            KeyFile = privateKeyPath,
            OutputPath = outputDir,
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(0); // Success
        TestConsole.Output.ShouldContain("Found 3 file(s) to decrypt");
        TestConsole.Output.ShouldContain("✓ Success: 3");

        // Verify all files were decrypted
        string decrypted1 = await FileSystem.File.ReadAllTextAsync(
            Path.Join(outputDir, "app1.decrypted.log"),
            TestContext.Current.CancellationToken
        );
        string decrypted2 = await FileSystem.File.ReadAllTextAsync(
            Path.Join(outputDir, "app2.decrypted.log"),
            TestContext.Current.CancellationToken
        );
        string decrypted3 = await FileSystem.File.ReadAllTextAsync(
            Path.Join(outputDir, "app3.decrypted.log"),
            TestContext.Current.CancellationToken
        );

        decrypted1.ShouldBe(LogContent1);
        decrypted2.ShouldBe(LogContent2);
        decrypted3.ShouldBe(LogContent3);
    }

    [Fact]
    public async Task ExecuteAsync_WithDirectoryPath_DecryptsAllFilesInDirectory()
    {
        // Arrange
        const string LogContent1 = "2024-11-26 14:00:00 [INF] Log file 1\n";
        const string LogContent2 = "2024-11-26 14:00:01 [INF] Log file 2\n";

        string privateKeyPath = Path.Join("keys", "private_key.xml");
        string logsDir = Path.Join("logs");
        string encryptedFile1 = Path.Join(logsDir, "app.log");
        string encryptedFile2 = Path.Join(logsDir, "service.log");

        (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair();

        FileSystem.AddFile(privateKeyPath, new MockFileData(privateKey));
        FileSystem.AddFile(
            encryptedFile1,
            new MockFileData(CreateEncryptedLogFile(LogContent1, publicKey))
        );
        FileSystem.AddFile(
            encryptedFile2,
            new MockFileData(CreateEncryptedLogFile(LogContent2, publicKey))
        );

        DecryptCommand command = new(TestConsole, FileSystem);
        DecryptCommand.Settings settings = new()
        {
            InputPath = logsDir,
            KeyFile = privateKeyPath,
            Pattern = "*.log",
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(0); // Success
        TestConsole.Output.ShouldContain("Found 2 file(s) to decrypt");
        TestConsole.Output.ShouldContain("✓ Success: 2");

        // Files should be decrypted in place with .decrypted extension
        FileSystem.File.Exists(Path.Join(logsDir, "app.decrypted.log")).ShouldBeTrue();
        FileSystem.File.Exists(Path.Join(logsDir, "service.decrypted.log")).ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithRecursiveOption_DecryptsFilesInSubdirectories()
    {
        // Arrange
        const string LogContent1 = "2024-11-26 14:00:00 [INF] Root log\n";
        const string LogContent2 = "2024-11-26 14:00:01 [INF] Sub log\n";

        string privateKeyPath = Path.Join("keys", "private_key.xml");
        string logsDir = Path.Join("logs");
        string subDir = Path.Join(logsDir, "subdir");
        string encryptedFile1 = Path.Join(logsDir, "app.log");
        string encryptedFile2 = Path.Join(subDir, "sub.log");

        (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair();

        FileSystem.AddFile(privateKeyPath, new MockFileData(privateKey));
        FileSystem.AddFile(
            encryptedFile1,
            new MockFileData(CreateEncryptedLogFile(LogContent1, publicKey))
        );
        FileSystem.AddFile(
            encryptedFile2,
            new MockFileData(CreateEncryptedLogFile(LogContent2, publicKey))
        );

        DecryptCommand command = new(TestConsole, FileSystem);
        DecryptCommand.Settings settings = new()
        {
            InputPath = logsDir,
            KeyFile = privateKeyPath,
            Pattern = "*.log",
            Recursive = true,
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(0); // Success
        TestConsole.Output.ShouldContain("Found 2 file(s) to decrypt");
        TestConsole.Output.ShouldContain("✓ Success: 2");

        FileSystem.File.Exists(Path.Join(logsDir, "app.decrypted.log")).ShouldBeTrue();
        FileSystem.File.Exists(Path.Join(subDir, "sub.decrypted.log")).ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_OverwritesExistingFiles()
    {
        // Arrange
        const string LogContent = "2024-11-26 14:00:00 [INF] New content\n";
        const string OldContent = "Old content";

        string privateKeyPath = Path.Join("keys", "private_key.xml");
        string encryptedFile = Path.Join("logs", "app.log");
        string decryptedFile = Path.Join("logs", "app.decrypted.log");

        (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair();

        FileSystem.AddFile(privateKeyPath, new MockFileData(privateKey));
        FileSystem.AddFile(
            encryptedFile,
            new MockFileData(CreateEncryptedLogFile(LogContent, publicKey))
        );
        FileSystem.AddFile(decryptedFile, new MockFileData(OldContent));

        DecryptCommand command = new(TestConsole, FileSystem);
        DecryptCommand.Settings settings = new()
        {
            InputPath = encryptedFile,
            KeyFile = privateKeyPath,
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
        content.ShouldBe(LogContent); // Should be new content, not old
        TestConsole.Output.ShouldContain("will be overwritten");
        TestConsole.Output.ShouldContain("✓ Decrypted:");
    }

    [Fact]
    public async Task ExecuteAsync_WithMixedSuccessAndFailure_ReturnsPartialSuccess()
    {
        // Arrange - One valid file and one file that will cause IOException
        const string LogContent1 = "2024-11-26 14:00:00 [INF] Valid log\n";

        string privateKeyPath = Path.Join("keys", "private_key.xml");
        string validFile = Path.Join("logs", "valid.log");
        string lockedFile = Path.Join("logs", "locked.log");

        (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair();

        FileSystem.AddFile(privateKeyPath, new MockFileData(privateKey));
        FileSystem.AddFile(
            validFile,
            new MockFileData(CreateEncryptedLogFile(LogContent1, publicKey))
        );
        FileSystem.AddFile(
            lockedFile,
            new MockFileData(CreateEncryptedLogFile(LogContent1, publicKey))
        );

        // Mock the file system to throw IOException when trying to read the locked file
        IFileSystem mockFs = Substitute.For<IFileSystem>();
        mockFs.File.Exists(privateKeyPath).Returns(true);
        mockFs.File.Exists(validFile).Returns(true);
        mockFs.File.Exists(lockedFile).Returns(true);
        mockFs
            .File.ReadAllTextAsync(privateKeyPath, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(privateKey));

        mockFs
            .Directory.GetFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>())
            .Returns([validFile, lockedFile]);

        mockFs.Path.GetFileName(validFile).Returns(Path.GetFileName(validFile));
        mockFs.Path.GetFileName(lockedFile).Returns(Path.GetFileName(lockedFile));
        mockFs
            .Path.GetFileNameWithoutExtension(Arg.Any<string>())
            .Returns(x => Path.GetFileNameWithoutExtension((string)x[0]));
        mockFs.Path.GetExtension(Arg.Any<string>()).Returns(x => Path.GetExtension((string)x[0]));
        mockFs
            .Path.GetDirectoryName(Arg.Any<string>())
            .Returns(x => Path.GetDirectoryName((string)x[0]));
        mockFs
            .Path.Combine(Arg.Any<string>(), Arg.Any<string>())
            .Returns(x => Path.Combine((string)x[0], (string)x[1]));

        mockFs.File.OpenRead(validFile).Returns(_ => FileSystem.File.OpenRead(validFile));
        mockFs
            .File.When(f => f.OpenRead(lockedFile))
            .Do(_ => throw new IOException("File is locked"));

        mockFs
            .File.Create(Arg.Is<string>(s => s.Contains("valid")))
            .Returns(x => FileSystem.File.Create((string)x[0]));
        mockFs.File.Exists(Arg.Is<string>(s => s.Contains(".decrypted"))).Returns(false);
        mockFs.Directory.Exists(Arg.Any<string>()).Returns(true);

        DecryptCommand command = new(TestConsole, mockFs);
        DecryptCommand.Settings settings = new()
        {
            InputPath = Path.Join("logs", "*.log"),
            KeyFile = privateKeyPath,
            ErrorMode = ErrorHandlingMode.Skip,
            ContinueOnError = true,
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
    public async Task ExecuteAsync_WithCustomPattern_OnlyDecryptsMatchingFiles()
    {
        // Arrange
        const string LogContent = "2024-11-26 14:00:00 [INF] Test log\n";

        string privateKeyPath = Path.Join("keys", "private_key.xml");
        string logsDir = Path.Join("logs");
        string txtFile = Path.Join(logsDir, "app.txt");
        string logFile = Path.Join(logsDir, "app.log");

        (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair();

        FileSystem.AddFile(privateKeyPath, new MockFileData(privateKey));
        FileSystem.AddFile(
            txtFile,
            new MockFileData(CreateEncryptedLogFile(LogContent, publicKey))
        );
        FileSystem.AddFile(
            logFile,
            new MockFileData(CreateEncryptedLogFile(LogContent, publicKey))
        );

        DecryptCommand command = new(TestConsole, FileSystem);
        DecryptCommand.Settings settings = new()
        {
            InputPath = logsDir,
            KeyFile = privateKeyPath,
            Pattern = "*.txt", // Only match .txt files
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(0); // Success
        TestConsole.Output.ShouldContain("Found 1 file(s) to decrypt");
        FileSystem.File.Exists(Path.Join(logsDir, "app.decrypted.txt")).ShouldBeTrue();
        FileSystem.File.Exists(Path.Join(logsDir, "app.decrypted.log")).ShouldBeFalse(); // .log file should not be decrypted
    }

    [Fact]
    public async Task ExecuteAsync_WithSingleFileAndOutputDirectory_CreatesDecryptedFileInDirectory()
    {
        // Arrange
        const string LogContent = "2024-11-26 14:00:00 [INF] Test log\n";

        string privateKeyPath = Path.Join("keys", "private_key.xml");
        string encryptedFile = Path.Join("logs", "app.log");
        string outputDir = Path.Join("decrypted");

        (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair();

        FileSystem.AddFile(privateKeyPath, new MockFileData(privateKey));
        FileSystem.AddFile(
            encryptedFile,
            new MockFileData(CreateEncryptedLogFile(LogContent, publicKey))
        );
        FileSystem.AddDirectory(outputDir);

        DecryptCommand command = new(TestConsole, FileSystem);
        DecryptCommand.Settings settings = new()
        {
            InputPath = encryptedFile,
            KeyFile = privateKeyPath,
            OutputPath = outputDir,
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(0); // Success
        // When output is an existing directory with single file input, file should be created inside the directory
        FileSystem.File.Exists(Path.Join(outputDir, "app.decrypted.log")).ShouldBeTrue();
        string content = await FileSystem.File.ReadAllTextAsync(
            Path.Join(outputDir, "app.decrypted.log"),
            TestContext.Current.CancellationToken
        );
        content.ShouldBe(LogContent);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoMatchingFiles_ReturnsSuccessWithWarning()
    {
        // Arrange
        string privateKeyPath = Path.Join("keys", "private_key.xml");
        string logsDir = Path.Join("logs");

        (string _, string privateKey) = EncryptionUtils.GenerateRsaKeyPair();

        FileSystem.AddFile(privateKeyPath, new MockFileData(privateKey));
        FileSystem.AddDirectory(logsDir);

        DecryptCommand command = new(TestConsole, FileSystem);
        DecryptCommand.Settings settings = new()
        {
            InputPath = Path.Join(logsDir, "*.log"),
            KeyFile = privateKeyPath,
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
    public async Task ExecuteAsync_WithEmptyInputPath_ReturnsError()
    {
        // Arrange
        string privateKeyPath = Path.Join("keys", "private_key.xml");
        (string _, string privateKey) = EncryptionUtils.GenerateRsaKeyPair();
        FileSystem.AddFile(privateKeyPath, new MockFileData(privateKey));

        DecryptCommand command = new(TestConsole, FileSystem);
        DecryptCommand.Settings settings = new() { InputPath = "", KeyFile = privateKeyPath };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(1); // Error
        TestConsole.Output.ShouldContain("✗ Error: Input path is required");
    }

    [Fact]
    public async Task ExecuteAsync_CreatesOutputDirectoryIfNotExists()
    {
        // Arrange
        const string LogContent = "2024-11-26 14:00:00 [INF] Test log\n";

        string privateKeyPath = Path.Join("keys", "private_key.xml");
        string encryptedFile = Path.Join("logs", "app.log");
        string outputFile = Path.Join("new-dir", "output.log");

        (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair();

        FileSystem.AddFile(privateKeyPath, new MockFileData(privateKey));
        FileSystem.AddFile(
            encryptedFile,
            new MockFileData(CreateEncryptedLogFile(LogContent, publicKey))
        );

        DecryptCommand command = new(TestConsole, FileSystem);
        DecryptCommand.Settings settings = new()
        {
            InputPath = encryptedFile,
            KeyFile = privateKeyPath,
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

    /// <summary>
    /// Helper method to create an encrypted log file using EncryptedStream
    /// </summary>
    private static byte[] CreateEncryptedLogFile(string logContent, string rsaPublicKey)
    {
        using MemoryStream memoryStream = new();
        using RSA rsa = RSA.Create();
        rsa.FromXmlString(rsaPublicKey);

        using (EncryptedStream encryptedStream = new(memoryStream, rsa))
        {
            byte[] logBytes = Encoding.UTF8.GetBytes(logContent);
            encryptedStream.Write(logBytes, 0, logBytes.Length);
            encryptedStream.Flush();
        }

        return memoryStream.ToArray();
    }
}
