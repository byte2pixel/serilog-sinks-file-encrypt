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
            KeyFile = privateKeyPath,
            EncryptedFile = encryptedFilePath,
            OutputFile = decryptedFilePath,
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

        TestConsole.Output.ShouldContain("Successfully decrypted log file!");
        TestConsole.Output.ShouldContain("Reading private key from:");
        TestConsole.Output.ShouldContain("Decrypting log file:");
        TestConsole.Output.ShouldContain("Decrypted content written to:");
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
            EncryptedFile = Path.Join("logs", "encrypted.log"),
            OutputFile = Path.Join("logs", "decrypted.log"),
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
            KeyFile = Path.Join("keys", "private_key.xml"),
            EncryptedFile = Path.Join("logs", "missing.log"),
            OutputFile = Path.Join("logs", "decrypted.log"),
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(1); // Error
        TestConsole.Output.ShouldContain("Error: Encrypted file");
        TestConsole.Output.ShouldContain("does not exist");
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
            EncryptedFile = encryptedFilePath,
            OutputFile = decryptedFilePath,
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
            EncryptedFile = encryptedFilePath,
            OutputFile = decryptedFilePath,
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
            KeyFile = privateKeyPath,
            EncryptedFile = encryptedFilePath,
            OutputFile = decryptedFilePath,
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
            EncryptedFile = encryptedFilePath,
            OutputFile = decryptedFilePath,
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
            EncryptedFile = encryptedFilePath,
            OutputFile = decryptedFilePath,
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
            EncryptedFile = encryptedFilePath,
            OutputFile = decryptedFilePath,
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(1); // Error
        TestConsole.Output.ShouldContain("Access denied:");
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
            EncryptedFile = encryptedFilePath,
            OutputFile = decryptedFilePath,
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
            EncryptedFile = encryptedFilePath,
            OutputFile = decryptedFilePath,
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
            KeyFile = privateKeyPath,
            EncryptedFile = encryptedFilePath,
            OutputFile = decryptedFilePath,
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
        TestConsole.Output.ShouldContain("Invalid file:");
        TestConsole.Output.ShouldContain("does not contain valid encryption markers");
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
