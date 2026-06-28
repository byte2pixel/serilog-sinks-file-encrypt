namespace Serilog.Sinks.File.Decrypt.Tests;

/// <summary>
/// This class tests the edge cases of LogReader.
/// The majority of LogReader's testing is done in the integration tests,
/// where LogWriter and LogReader are tested together.
/// </summary>
public class LogReaderTests
{
    [Fact]
    public void GivenStreamNull_WhenConstructing_ThenThrows()
    {
        // Arrange
        DecryptionOptions options = new() { KeyProvider = Substitute.For<IKeyProvider>() };

        // Act & Assert
        Should
            .Throw<ArgumentNullException>(() =>
            {
                using LogReader reader = new(null!, options);
            })
            .Message.ShouldContain("input");
    }

    [Fact]
    public void GivenDecryptionOptionsNull_WhenConstructing_ThenThrows()
    {
        // Arrange
        using MemoryStream ms = new();

        // Act & Assert
        Should
            .Throw<ArgumentNullException>(() =>
            {
                using LogReader reader = new(ms, null!);
            })
            .Message.ShouldContain("options");
    }

    [Fact]
    public void GivenNullKeyProvider_WhenConstructing_ThenThrows()
    {
        // Arrange
        using MemoryStream ms = new();
        DecryptionOptions options = new() { KeyProvider = null! };

        // Act & Assert
        Should
            .Throw<InvalidOperationException>(() =>
            {
                using LogReader reader = new(ms, options);
            })
            .Message.ShouldStartWith("A KeyProvider must be");
    }

    [Fact]
    public void GivenUnknownErrorHandling_WhenConstructing_ThenThrows()
    {
        // Arrange
        using MemoryStream ms = new();
        (_, string privateKey) = CryptographicUtils.GenerateRsaKeyPair();
        var keyMap = new Dictionary<string, string>() { { "", privateKey } };
        using LocalKeyProvider keyProvider = new(keyMap);
        DecryptionOptions options = new()
        {
            KeyProvider = keyProvider,
            ErrorHandlingMode = (ErrorHandlingMode)999,
        };

        // Act & Assert
        Should
            .Throw<InvalidOperationException>(() =>
            {
                using LogReader reader = new(ms, options);
            })
            .Message.ShouldContain("Invalid error handling mode: 999");
    }
}
