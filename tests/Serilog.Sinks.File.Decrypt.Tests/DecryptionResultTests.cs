namespace Serilog.Sinks.File.Decrypt.Tests;

/// <summary>
/// Tests for the computed signals on <see cref="DecryptionResult"/>, in particular
/// <see cref="DecryptionResult.NothingDecrypted"/> (#84).
/// </summary>
public sealed class DecryptionResultTests : EncryptionTestBase
{
    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(1, 0, false)]
    [InlineData(0, 1, false)]
    [InlineData(1, 5, false)]
    public void NothingDecrypted_ReflectsSessionAndMessageCounts(
        int sessions,
        int messages,
        bool expected
    )
    {
        DecryptionResult result = new()
        {
            DecryptedSessions = sessions,
            DecryptedMessages = messages,
        };

        result.NothingDecrypted.ShouldBe(expected);
    }

    [Fact]
    public void NothingDecrypted_TrueEvenWithRecordedFailures()
    {
        DecryptionResult result = new() { FailedHeaders = 2, FailedMessages = 3 };

        result.NothingDecrypted.ShouldBeTrue();
    }

    [Fact]
    public async Task SkipMode_WrongKey_ReportsNothingDecrypted()
    {
        // Arrange — encrypt with the test base key, decrypt with a different key
        MemoryStream encryptedStream = await CreateEncryptedStreamAsync("secret message");
        (_, string wrongPrivateKey) = CryptographicUtils.GenerateRsaKeyPair(format: KeyFormat.Pem);
        using var output = new MemoryStream();
        var options = new DecryptionOptions
        {
            KeyProvider = new LocalKeyProvider("", wrongPrivateKey),
            ErrorHandlingMode = ErrorHandlingMode.Skip,
        };
        using var reader = new LogReader(encryptedStream, options);

        // Act
        DecryptionResult result = await reader.DecryptToStreamAsync(
            output,
            TestContext.Current.CancellationToken
        );

        // Assert — the empty-output signal library callers can react to
        result.NothingDecrypted.ShouldBeTrue();
        result.FailedHeaders.ShouldBeGreaterThan(0);
        output.Length.ShouldBe(0);
    }

    [Fact]
    public async Task SkipMode_EmptyStream_ReportsNothingDecrypted()
    {
        // Arrange
        using var input = new MemoryStream();
        using var output = new MemoryStream();
        var options = new DecryptionOptions
        {
            KeyProvider = DecryptOptions.KeyProvider,
            ErrorHandlingMode = ErrorHandlingMode.Skip,
        };
        using var reader = new LogReader(input, options);

        // Act
        DecryptionResult result = await reader.DecryptToStreamAsync(
            output,
            TestContext.Current.CancellationToken
        );

        // Assert
        result.NothingDecrypted.ShouldBeTrue();
        result.FailedHeaders.ShouldBe(0);
        result.FailedMessages.ShouldBe(0);
    }
}
