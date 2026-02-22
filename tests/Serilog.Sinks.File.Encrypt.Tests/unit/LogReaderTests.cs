namespace Serilog.Sinks.File.Encrypt.Tests;

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
        DecryptionOptions options = new() { DecryptionKeys = [] };

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
    public void GivenNullDecryptionKeys_WhenConstructing_ThenThrows()
    {
        // Arrange
        using MemoryStream ms = new();
        DecryptionOptions options = new() { DecryptionKeys = null! };

        // Act & Assert
        Should
            .Throw<InvalidOperationException>(() =>
            {
                using LogReader reader = new(ms, options);
            })
            .Message.ShouldContain("At least one decryption key must be provided");
    }

    [Fact]
    public void GivenEmptyDecryptionKeys_WhenConstructing_ThenThrows()
    {
        // Arrange
        using MemoryStream ms = new();
        DecryptionOptions options = new() { DecryptionKeys = [] };

        // Act & Assert
        Should
            .Throw<InvalidOperationException>(() =>
            {
                using LogReader reader = new(ms, options);
            })
            .Message.ShouldContain("At least one decryption key must be provided");
    }

    [Fact]
    public void GivenUnknownErrorHandling_WhenConstructing_ThenThrows()
    {
        // Arrange
        using MemoryStream ms = new();
        DecryptionOptions options = TestUtils.GetDecryptionOptions(
            "dummy-public",
            mode: (ErrorHandlingMode)999
        );

        // Act & Assert
        Should
            .Throw<InvalidOperationException>(() =>
            {
                using LogReader reader = new(ms, options);
            })
            .Message.ShouldContain("Invalid error handling mode: 999");
    }
}
