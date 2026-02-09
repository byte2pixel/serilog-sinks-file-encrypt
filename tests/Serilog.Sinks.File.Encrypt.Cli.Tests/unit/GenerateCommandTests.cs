using Serilog.Sinks.File.Encrypt.Models;

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

    [Fact]
    public void Execute_WithXmlFormat_GeneratesKeyPairSuccessfully()
    {
        // Arrange
        GenerateCommand command = new(TestConsole, FileSystem);
        string outputPath = Path.Join("test-keys");
        GenerateCommand.Settings settings = new() { OutputPath = outputPath, KeySize = 2048, Format = KeyFormat.Xml };

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

    [Fact]
    public void Execute_WithPemFormat_GeneratesKeyPairSuccessfully()
    {
        // Arrange
        GenerateCommand command = new(TestConsole, FileSystem);
        string outputPath = Path.Join("test-keys");
        GenerateCommand.Settings settings = new() { OutputPath = outputPath, KeySize = 2048, Format = KeyFormat.Pem };

        // Act
        int result = command.Execute(
            new CommandContext(Arguments, Remaining, "generate", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(0); // Success

        FileSystem.Directory.Exists(outputPath).ShouldBeTrue();

        string privateKeyPath = Path.Join(outputPath, "private_key.pem");
        FileSystem.File.Exists(privateKeyPath).ShouldBeTrue();
        string privateKey = FileSystem.File.ReadAllText(privateKeyPath);
        privateKey.ShouldNotBeNullOrEmpty();
        privateKey.ShouldContain("-----BEGIN RSA PRIVATE KEY-----");

        string publicKeyPath = Path.Join(outputPath, "public_key.pem");
        FileSystem.File.Exists(publicKeyPath).ShouldBeTrue();
        string publicKey = FileSystem.File.ReadAllText(publicKeyPath);
        publicKey.ShouldNotBeNullOrEmpty();
        publicKey.ShouldContain("-----BEGIN RSA PUBLIC KEY-----");
        publicKey.ShouldNotContain("PRIVATE"); // Public key should not contain private parameters

        TestConsole.Output.ShouldContain("Successfully generated RSA key pair!");
        TestConsole.Output.ShouldContain("Private Key:");
        TestConsole.Output.ShouldContain("Public Key:");
        TestConsole.Output.ShouldContain("Keep your private key secure");
    }

    [Fact]
    public void Execute_WhenDirectoryCreationFails_ReturnsErrorAndDisplaysMessage()
    {
        // Arrange
        string outputPath = Path.Join("protected-directory");

        // Use NSubstitute to create a mock that throws UnauthorizedAccessException
        IFileSystem mockFs = Substitute.For<IFileSystem>();
        mockFs.Directory.Exists(Arg.Any<string>()).Returns(false);
        mockFs
            .Directory.When(d => d.CreateDirectory(Arg.Any<string>()))
            .Do(_ => throw new UnauthorizedAccessException("Access denied to create directory"));

        GenerateCommand command = new(TestConsole, mockFs);
        GenerateCommand.Settings settings = new() { OutputPath = outputPath, KeySize = 2048 };

        // Act
        int result = command.Execute(
            new CommandContext(Arguments, Remaining, "generate", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(1); // Error
        TestConsole.Output.ShouldContain("Access denied to output path:");
        TestConsole.Output.ShouldContain("Access denied to create directory");
    }

    [Fact]
    public void Execute_WhenWritingPrivateKeyFails_ReturnsErrorAndDisplaysMessage()
    {
        // Arrange
        string outputPath = Path.Join("test-keys");

        // Use NSubstitute to create a mock that throws IOException when writing private key
        IFileSystem mockFs = Substitute.For<IFileSystem>();
        mockFs.Directory.Exists(Arg.Any<string>()).Returns(true);
        mockFs
            .Path.Join(Arg.Any<string>(), Arg.Any<string>())
            .Returns(x => Path.Join((string)x[0], (string)x[1]));

        string privateKeyPath = Path.Join(outputPath, "private_key.xml");

        mockFs
            .File.When(f => f.WriteAllText(privateKeyPath, Arg.Any<string>()))
            .Do(_ => throw new IOException("Disk full or write error"));

        GenerateCommand command = new(TestConsole, mockFs);
        GenerateCommand.Settings settings = new() { OutputPath = outputPath, KeySize = 2048 };

        // Act
        int result = command.Execute(
            new CommandContext(Arguments, Remaining, "generate", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(1); // Error
        TestConsole.Output.ShouldContain("Error writing key files:");
        TestConsole.Output.ShouldContain("Disk full or write error");
    }

    [Fact]
    public void Execute_WhenInvalidKeySizeProvided_ReturnsErrorAndDisplaysMessage()
    {
        // Arrange
        string outputPath = Path.Join("test-keys");

        // Use a real file system but an invalid key size that will cause CryptographicException
        GenerateCommand command = new(TestConsole, FileSystem);
        GenerateCommand.Settings settings = new()
        {
            OutputPath = outputPath,
            KeySize = 123, // Invalid key size - RSA requires specific sizes
        };

        // Act
        int result = command.Execute(
            new CommandContext(Arguments, Remaining, "generate", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(1); // Error
        TestConsole.Output.ShouldContain("Error generating RSA key pair:");
    }

    [Fact]
    public void Execute_CreatesDirectoryWhenItDoesNotExist()
    {
        // Arrange
        string outputPath = Path.Join("new-test-keys");

        GenerateCommand command = new(TestConsole, FileSystem);
        GenerateCommand.Settings settings = new() { OutputPath = outputPath, KeySize = 2048 };

        FileSystem.Directory.Exists(outputPath).ShouldBeFalse();

        // Act
        int result = command.Execute(
            new CommandContext(Arguments, Remaining, "generate", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(0);
        FileSystem.Directory.Exists(outputPath).ShouldBeTrue();
        TestConsole.Output.ShouldContain("Created directory:");
        TestConsole.Output.ShouldContain(outputPath);
    }
}
