using Serilog.Sinks.File.Encrypt.Models;
using Spectre.Console;

namespace Serilog.Sinks.File.Encrypt.Cli.Tests;

public class GenerateCommandTests : CommandTestBase
{
    private const string TestPassphrase = "test-passphrase";

    private readonly IPassphraseResolver _passphraseResolver =
        Substitute.For<IPassphraseResolver>();

    public GenerateCommandTests()
    {
        // Default: behave as if a passphrase source resolved successfully
        _passphraseResolver
            .Resolve(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<bool>())
            .Returns(TestPassphrase);
    }

    private GenerateCommand GetSut(IFileSystem? fileSystem = null) =>
        new(Writer, fileSystem ?? FileSystem, _passphraseResolver);

    [Fact]
    public void Execute_WithDefaultSettings_GeneratesEncryptedPemKeyPair()
    {
        // Arrange — 6.0.0 defaults: Pem format, passphrase-encrypted private key
        GenerateCommand command = GetSut();
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

        string privateKeyPath = Path.Join(outputPath, "private_key.pem");
        FileSystem.File.Exists(privateKeyPath).ShouldBeTrue();
        string privateKey = FileSystem.File.ReadAllText(privateKeyPath);
        privateKey.ShouldContain("-----BEGIN ENCRYPTED PRIVATE KEY-----");

        string publicKeyPath = Path.Join(outputPath, "public_key.pem");
        FileSystem.File.Exists(publicKeyPath).ShouldBeTrue();
        string publicKey = FileSystem.File.ReadAllText(publicKeyPath);
        publicKey.ShouldContain("-----BEGIN RSA PUBLIC KEY-----");
        publicKey.ShouldNotContain("PRIVATE");

        // Round trip: the encrypted key imports with the passphrase
        using var rsa = RSA.Create();
        Should.NotThrow(() => rsa.ImportFromEncryptedPem(privateKey, TestPassphrase));

        TestConsole.Output.ShouldContain("Successfully generated RSA key pair!");
        TestConsole.Output.ShouldContain("passphrase-encrypted");
        TestConsole.Output.ShouldContain("Keep your private key secure");
    }

    [Fact]
    public void Execute_NoPassphraseSourceNonInteractive_FailsWithUsageError()
    {
        // Arrange — resolver finds nothing and cannot prompt
        _passphraseResolver
            .Resolve(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<bool>())
            .Returns((string?)null);
        GenerateCommand command = GetSut();
        GenerateCommand.Settings settings = new() { OutputPath = "keys" };

        // Act
        int result = command.Execute(
            new CommandContext(Arguments, Remaining, "generate", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(ExitCodes.UsageError);
        TestConsole.Output.ShouldContain("No passphrase source available");
        TestConsole.Output.ShouldContain("--plaintext");
        FileSystem.AllFiles.ShouldBeEmpty();
    }

    [Fact]
    public void Execute_PassphraseSourceFails_FailsWithUsageError()
    {
        // Arrange — e.g. --passphrase-env points at an unset variable
        _passphraseResolver
            .Resolve(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<bool>())
            .Returns(_ =>
                throw new PassphraseResolutionException(
                    "Environment variable 'MISSING' is not set or empty."
                )
            );
        GenerateCommand command = GetSut();
        GenerateCommand.Settings settings = new()
        {
            OutputPath = "keys",
            PassphraseEnv = "MISSING",
        };

        // Act
        int result = command.Execute(
            new CommandContext(Arguments, Remaining, "generate", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(ExitCodes.UsageError);
        TestConsole.Output.ShouldContain("'MISSING' is not set or empty");
    }

    [Fact]
    public void Execute_WithPlaintext_SkipsPassphraseResolution()
    {
        // Arrange
        GenerateCommand command = GetSut();
        string outputPath = Path.Join("test-keys");
        GenerateCommand.Settings settings = new()
        {
            OutputPath = outputPath,
            KeySize = 2048,
            Plaintext = true,
        };

        // Act
        int result = command.Execute(
            new CommandContext(Arguments, Remaining, "generate", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(0);
        _passphraseResolver
            .DidNotReceive()
            .Resolve(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<bool>());
        string privateKey = FileSystem.File.ReadAllText(Path.Join(outputPath, "private_key.pem"));
        privateKey.ShouldContain("-----BEGIN RSA PRIVATE KEY-----");
        TestConsole.Output.ShouldContain("NOT passphrase-protected");
    }

    [Fact]
    public void Settings_XmlWithoutPlaintext_FailsValidation()
    {
        GenerateCommand.Settings settings = new() { OutputPath = "keys", Format = KeyFormat.Xml };

        ValidationResult result = settings.Validate();

        result.Successful.ShouldBeFalse();
        result.Message.ShouldNotBeNull();
        result.Message.ShouldContain("--plaintext");
    }

    [Fact]
    public void Settings_PlaintextWithPassphraseSource_FailsValidation()
    {
        GenerateCommand.Settings settings = new()
        {
            OutputPath = "keys",
            Plaintext = true,
            PassphraseEnv = "SOME_VAR",
        };

        ValidationResult result = settings.Validate();

        result.Successful.ShouldBeFalse();
    }

    [Fact]
    public void Execute_ExistingKeyFilesWithoutForce_RefusesAndExitsUsageError()
    {
        // Arrange — a private key already exists at the target path
        string outputPath = Path.Join("test-keys");
        string privateKeyPath = Path.Join(outputPath, "private_key.pem");
        const string ExistingKey = "irreplaceable key material";
        FileSystem.AddFile(privateKeyPath, new MockFileData(ExistingKey));
        GenerateCommand command = GetSut();
        GenerateCommand.Settings settings = new() { OutputPath = outputPath };

        // Act
        int result = command.Execute(
            new CommandContext(Arguments, Remaining, "generate", null),
            settings,
            CancellationToken.None
        );

        // Assert — refused, existing key untouched
        result.ShouldBe(ExitCodes.UsageError);
        FileSystem.File.ReadAllText(privateKeyPath).ShouldBe(ExistingKey);
        TestConsole.Output.ShouldContain("key file(s) already exist");
        TestConsole.Output.ShouldContain("--force");
        TestConsole.Output.ShouldNotContain("Successfully generated RSA key pair!");
    }

    [Fact]
    public void Execute_ExistingKeyFilesWithForce_Overwrites()
    {
        // Arrange
        string outputPath = Path.Join("test-keys");
        string privateKeyPath = Path.Join(outputPath, "private_key.pem");
        FileSystem.AddFile(privateKeyPath, new MockFileData("old key"));
        GenerateCommand command = GetSut();
        GenerateCommand.Settings settings = new() { OutputPath = outputPath, Force = true };

        // Act
        int result = command.Execute(
            new CommandContext(Arguments, Remaining, "generate", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(0);
        FileSystem
            .File.ReadAllText(privateKeyPath)
            .ShouldContain("-----BEGIN ENCRYPTED PRIVATE KEY-----");
        TestConsole.Output.ShouldContain("Successfully generated RSA key pair!");
    }

    [Fact]
    public void Execute_WithXmlFormatAndPlaintext_GeneratesKeyPairSuccessfully()
    {
        // Arrange
        GenerateCommand command = GetSut();
        string outputPath = Path.Join("test-keys");
        GenerateCommand.Settings settings = new()
        {
            OutputPath = outputPath,
            KeySize = 2048,
            Format = KeyFormat.Xml,
            Plaintext = true,
        };

        // Act
        int result = command.Execute(
            new CommandContext(Arguments, Remaining, "generate", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(0); // Success

        string privateKeyPath = Path.Join(outputPath, "private_key.xml");
        FileSystem.File.Exists(privateKeyPath).ShouldBeTrue();
        string privateKey = FileSystem.File.ReadAllText(privateKeyPath);
        privateKey.ShouldContain("<RSAKeyValue>");
        privateKey.ShouldContain("<D>"); // Private key should contain private parameters

        string publicKeyPath = Path.Join(outputPath, "public_key.xml");
        FileSystem.File.Exists(publicKeyPath).ShouldBeTrue();
        string publicKey = FileSystem.File.ReadAllText(publicKeyPath);
        publicKey.ShouldContain("<RSAKeyValue>");
        publicKey.ShouldNotContain("<D>"); // Public key should not contain private parameters

        TestConsole.Output.ShouldContain("Successfully generated RSA key pair!");
    }

    [Fact]
    public void Execute_WithPemFormatAndPlaintext_GeneratesUnencryptedPemKeyPair()
    {
        // Arrange
        GenerateCommand command = GetSut();
        string outputPath = Path.Join("test-keys");
        GenerateCommand.Settings settings = new()
        {
            OutputPath = outputPath,
            KeySize = 2048,
            Format = KeyFormat.Pem,
            Plaintext = true,
        };

        // Act
        int result = command.Execute(
            new CommandContext(Arguments, Remaining, "generate", null),
            settings,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(0); // Success

        string privateKey = FileSystem.File.ReadAllText(Path.Join(outputPath, "private_key.pem"));
        privateKey.ShouldContain("-----BEGIN RSA PRIVATE KEY-----");

        string publicKey = FileSystem.File.ReadAllText(Path.Join(outputPath, "public_key.pem"));
        publicKey.ShouldContain("-----BEGIN RSA PUBLIC KEY-----");
        publicKey.ShouldNotContain("PRIVATE");
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

        GenerateCommand command = GetSut(mockFs);
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

        string privateKeyPath = Path.Join(outputPath, "private_key.pem");

        mockFs
            .File.When(f => f.WriteAllText(privateKeyPath, Arg.Any<string>()))
            .Do(_ => throw new IOException("Disk full or write error"));

        GenerateCommand command = GetSut(mockFs);
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
        GenerateCommand command = GetSut();
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

        GenerateCommand command = GetSut();
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
