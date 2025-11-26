namespace Serilog.Sinks.File.Encrypt.Cli.Tests.unit;

public class GenerateCommandTests
{
    private static readonly string[] Arguments = [];
    private static readonly IRemainingArguments Remaining = Substitute.For<IRemainingArguments>();

    [Fact]
    public void Execute_WithDefaultSettings_GeneratesKeyPairSuccessfully()
    {
        // Arrange
        MockFileSystem fileSystem = new();
        TestConsole testConsole = new();

        GenerateCommand command = new(testConsole, fileSystem);
        GenerateCommand.Settings settings = new() { OutputPath = @"C:\test-keys", KeySize = 2048 };

        // Assert
        int result = command.Execute(
            new CommandContext(Arguments, Remaining, "generate", null),
            settings,
            CancellationToken.None
        );

        result.ShouldBe(0); // Success

        fileSystem.Directory.Exists(@"C:\test-keys").ShouldBeTrue();

        const string privateKeyPath = @"C:\test-keys\private_key.xml";
        fileSystem.File.Exists(privateKeyPath).ShouldBeTrue();
        string privateKey = fileSystem.File.ReadAllText(privateKeyPath);
        privateKey.ShouldNotBeNullOrEmpty();
        privateKey.ShouldContain("<RSAKeyValue>");
        privateKey.ShouldContain("<D>"); // Private key should contain private parameters

        const string publicKeyPath = @"C:\test-keys\public_key.xml";
        fileSystem.File.Exists(publicKeyPath).ShouldBeTrue();
        string publicKey = fileSystem.File.ReadAllText(publicKeyPath);
        publicKey.ShouldNotBeNullOrEmpty();
        publicKey.ShouldContain("<RSAKeyValue>");
        publicKey.ShouldNotContain("<D>"); // Public key should not contain private parameters

        testConsole.Output.ShouldContain("Successfully generated RSA key pair!");
        testConsole.Output.ShouldContain("Private Key:");
        testConsole.Output.ShouldContain("Public Key:");
        testConsole.Output.ShouldContain("Keep your private key secure");
    }
}
