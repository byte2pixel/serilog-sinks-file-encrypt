namespace Serilog.Sinks.File.Encrypt.Cli.Tests.unit;

public class GenerateCommandTests : CommandTestBase
{
    [Fact]
    public void Execute_WithDefaultSettings_GeneratesKeyPairSuccessfully()
    {
        // Arrange
        GenerateCommand command = new(TestConsole, FileSystem);
        string outputPath = Path.Join("test-keys");
        GenerateCommand.Settings settings = new() { OutputPath = outputPath, KeySize = 2048 };

        // Act
        int result = command.Execute(
            new CommandContext(Arguments, Remaining, "generate", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(0); // Success

        FileSystem.Directory.Exists(outputPath).ShouldBeTrue();

        string privateKeyPath = Path.Join(outputPath, "private_key.xml");
        FileSystem.File.Exists(privateKeyPath).ShouldBeTrue();
        string privateKey = FileSystem.File.ReadAllText(privateKeyPath);
        privateKey.ShouldNotBeNullOrEmpty();
        privateKey.ShouldContain("<RSAKeyValue>");
        privateKey.ShouldContain("<D>"); // Private key should contain private parameters

        string publicKeyPath = Path.Join(outputPath, "public_key.xml");
        FileSystem.File.Exists(publicKeyPath).ShouldBeTrue();
        string publicKey = FileSystem.File.ReadAllText(publicKeyPath);
        publicKey.ShouldNotBeNullOrEmpty();
        publicKey.ShouldContain("<RSAKeyValue>");
        publicKey.ShouldNotContain("<D>"); // Public key should not contain private parameters

        TestConsole.Output.ShouldContain("Successfully generated RSA key pair!");
        TestConsole.Output.ShouldContain("Private Key:");
        TestConsole.Output.ShouldContain("Public Key:");
        TestConsole.Output.ShouldContain("Keep your private key secure");
    }
}
