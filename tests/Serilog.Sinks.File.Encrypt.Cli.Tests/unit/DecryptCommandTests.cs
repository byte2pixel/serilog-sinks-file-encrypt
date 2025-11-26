namespace Serilog.Sinks.File.Encrypt.Cli.Tests.unit;

public class DecryptCommandTests
{
    private static readonly string[] Arguments = [];
    private static readonly IRemainingArguments Remaining = Substitute.For<IRemainingArguments>();

    [Fact]
    public async Task ExecuteAsync_WithValidEncryptedFile_DecryptsSuccessfully()
    {
        // Arrange
        const string testLogContent =
            "2024-11-26 14:00:00 [INF] Test log entry\n2024-11-26 14:00:01 [WRN] Warning message\n";
        const string privateKeyPath = @"C:\keys\private_key.xml";
        const string encryptedFilePath = @"C:\logs\encrypted.log";
        const string decryptedFilePath = @"C:\logs\decrypted.log";

        (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair();

        byte[] encryptedContent = CreateEncryptedLogFile(testLogContent, publicKey);

        MockFileSystem fileSystem = new();
        fileSystem.AddFile(privateKeyPath, new MockFileData(privateKey));
        fileSystem.AddFile(encryptedFilePath, new MockFileData(encryptedContent));

        TestConsole testConsole = new();
        DecryptCommand command = new(testConsole, fileSystem);
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

        fileSystem.File.Exists(decryptedFilePath).ShouldBeTrue();

        string decryptedContent = await fileSystem.File.ReadAllTextAsync(
            decryptedFilePath,
            TestContext.Current.CancellationToken
        );
        decryptedContent.ShouldBe(testLogContent);

        testConsole.Output.ShouldContain("Successfully decrypted log file!");
        testConsole.Output.ShouldContain("Reading private key from:");
        testConsole.Output.ShouldContain("Decrypting log file:");
        testConsole.Output.ShouldContain("Decrypted content written to:");
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingKeyFile_ReturnsError()
    {
        // Arrange
        MockFileSystem fileSystem = new();
        fileSystem.AddFile(@"C:\logs\encrypted.log", new MockFileData("dummy"));

        TestConsole testConsole = new();
        DecryptCommand command = new(testConsole, fileSystem);
        DecryptCommand.Settings settings = new()
        {
            KeyFile = @"C:\keys\missing_key.xml",
            EncryptedFile = @"C:\logs\encrypted.log",
            OutputFile = @"C:\logs\decrypted.log",
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(1); // Error
        testConsole.Output.ShouldContain("Error: Key file");
        testConsole.Output.ShouldContain("does not exist");
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingEncryptedFile_ReturnsError()
    {
        // Arrange
        MockFileSystem fileSystem = new();
        fileSystem.AddFile(
            Path.Combine("keys", "private_key.xml"),
            new MockFileData("<RSAKeyValue>test</RSAKeyValue>")
        );

        TestConsole testConsole = new();
        DecryptCommand command = new(testConsole, fileSystem);
        DecryptCommand.Settings settings = new()
        {
            KeyFile = Path.Combine("keys", "private_key.xml"),
            EncryptedFile = Path.Combine("logs", "missing.log"),
            OutputFile = Path.Combine("logs", "decrypted.log"),
        };

        // Act
        int result = await command.ExecuteAsync(
            new CommandContext(Arguments, Remaining, "decrypt", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(1); // Error
        testConsole.Output.ShouldContain("Error: Encrypted file");
        testConsole.Output.ShouldContain("does not exist");
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
