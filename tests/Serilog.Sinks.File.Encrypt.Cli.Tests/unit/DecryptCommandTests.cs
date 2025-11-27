namespace Serilog.Sinks.File.Encrypt.Cli.Tests.unit;

public class DecryptCommandTests : CommandTestBase
{
    [Fact]
    public async Task ExecuteAsync_WithValidEncryptedFile_DecryptsSuccessfully()
    {
        // Arrange
        const string testLogContent =
            "2024-11-26 14:00:00 [INF] Test log entry\n2024-11-26 14:00:01 [WRN] Warning message\n";
        string privateKeyPath = Path.Join("keys", "private_key.xml");
        string encryptedFilePath = Path.Join("logs", "encrypted.log");
        string decryptedFilePath = Path.Join("logs", "decrypted.log");

        (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair();

        byte[] encryptedContent = CreateEncryptedLogFile(testLogContent, publicKey);

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
        decryptedContent.ShouldBe(testLogContent);

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
        const string testLogContent = "2024-11-26 14:00:00 [INF] Test log entry\n";
        string privateKeyPath = Path.Join("keys", "private_key.xml");
        string encryptedFilePath = Path.Join("logs", "encrypted.log");
        string decryptedFilePath = Path.Join("logs", "decrypted.log");

        (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair();

        byte[] encryptedContent = CreateEncryptedLogFile(testLogContent, publicKey);

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
        const string testLogContent = "2024-11-26 14:00:00 [INF] Test log entry\n";
        string privateKeyPath = Path.Join("keys", "private_key.xml");
        string encryptedFilePath = Path.Join("logs", "encrypted.log");
        string decryptedFilePath = Path.Join("logs", "decrypted.log");
        string errorLogPath = Path.Join("logs", "errors.log");

        (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair();

        byte[] encryptedContent = CreateEncryptedLogFile(testLogContent, publicKey);

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
        const string testLogContent = "2024-11-26 14:00:00 [INF] Test log entry\n";
        string privateKeyPath = Path.Join("keys", "private_key.xml");
        string encryptedFilePath = Path.Join("logs", "encrypted.log");
        string decryptedFilePath = Path.Join("logs", "decrypted.log");

        (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair();

        byte[] encryptedContent = CreateEncryptedLogFile(testLogContent, publicKey);

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
