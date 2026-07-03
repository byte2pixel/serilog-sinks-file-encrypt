using System.Buffers.Binary;

namespace Serilog.Sinks.File.Decrypt.Tests;

/// <summary>
/// End-to-end integrity tests for the v2 format: per-frame associated-data binding
/// (header hash + frame sequence + frame type) and the authenticated end-of-log seal.
/// </summary>
public sealed class V2IntegrityTests : EncryptionTestBase
{
    private static readonly string[] _messages =
    [
        "message one\n",
        "message two\n",
        "message three\n",
    ];

    private DecryptionOptions ThrowOptions =>
        DecryptOptions with
        {
            ErrorHandlingMode = ErrorHandlingMode.ThrowException,
        };

    // ---------------------------------------------------------------------
    // Round trips
    // ---------------------------------------------------------------------

    [Fact]
    public async Task SealedV2_RoundTrip_ReportsSealed()
    {
        MemoryStream input = await CreateEncryptedStreamAsync(_messages);

        (string text, DecryptionResult result) = await DecryptWithResultAsync(input);

        text.ShouldBe(string.Concat(_messages));
        result.Sessions.Count.ShouldBe(1);
        result.Sessions[0].FormatVersion.ShouldBe(EncryptionConstants.FormatVersionV2);
        result.Sessions[0].SealStatus.ShouldBe(SealStatus.Sealed);
        result.Sessions[0].DeclaredFrameCount.ShouldBe((ulong)_messages.Length);
        result.Sessions[0].DecryptedMessages.ShouldBe(_messages.Length);
        result.AllSessionsSealed.ShouldBeTrue();
        result.UnsealedSessions.ShouldBe(0);
    }

    [Fact]
    public async Task SealedV2_RoundTrip_ThrowMode_Succeeds()
    {
        MemoryStream input = await CreateEncryptedStreamAsync(_messages);

        (string text, DecryptionResult result) = await DecryptWithResultAsync(
            input,
            ThrowOptions with
            {
                RequireSealed = true,
            }
        );

        text.ShouldBe(string.Concat(_messages));
        result.AllSessionsSealed.ShouldBeTrue();
    }

    [Fact]
    public async Task TwoSealedSessionsAppended_BothReportSealed()
    {
        MemoryStream input = await CreateEncryptedStreamAsync(_messages);
        input = await CreateAppendedMemoryStream(input, "appended session\n");

        (string text, DecryptionResult result) = await DecryptWithResultAsync(input);

        text.ShouldBe(string.Concat(_messages) + "appended session\n");
        result.Sessions.Count.ShouldBe(2);
        result.Sessions.ShouldAllBe(s => s.SealStatus == SealStatus.Sealed);
        result.Sessions[0].Index.ShouldBe(0);
        result.Sessions[1].Index.ShouldBe(1);
    }

    // ---------------------------------------------------------------------
    // Unsealed (crash) vs sealed
    // ---------------------------------------------------------------------

    [Fact]
    public async Task UnsealedTail_AllMessagesDecrypt_ReportedUnsealed()
    {
        // Writer never disposed = crash: frames flushed, no seal
        MemoryStream input = await CreateUnsealedEncryptedStreamAsync(_messages);

        (string text, DecryptionResult result) = await DecryptWithResultAsync(input);

        text.ShouldBe(string.Concat(_messages));
        result.FailedMessages.ShouldBe(0);
        result.Sessions.Count.ShouldBe(1);
        result.Sessions[0].SealStatus.ShouldBe(SealStatus.Unsealed);
        result.Sessions[0].DeclaredFrameCount.ShouldBeNull();
        result.UnsealedSessions.ShouldBe(1);
        result.AllSessionsSealed.ShouldBeFalse();
    }

    [Fact]
    public async Task UnsealedTail_RequireSealed_SkipMode_ReportsButDoesNotThrow()
    {
        MemoryStream input = await CreateUnsealedEncryptedStreamAsync(_messages);

        (string text, DecryptionResult result) = await DecryptWithResultAsync(
            input,
            DecryptOptions with
            {
                RequireSealed = true,
            }
        );

        text.ShouldBe(string.Concat(_messages));
        result.UnsealedSessions.ShouldBe(1);
    }

    [Fact]
    public async Task UnsealedTail_RequireSealed_ThrowMode_Throws()
    {
        MemoryStream input = await CreateUnsealedEncryptedStreamAsync(_messages);

        CryptographicException exception = await Should.ThrowAsync<CryptographicException>(
            async () =>
                await DecryptWithResultAsync(input, ThrowOptions with { RequireSealed = true })
        );
        exception.Message.ShouldContain("not verified as sealed");
    }

    // ---------------------------------------------------------------------
    // Truncation of a sealed log
    // ---------------------------------------------------------------------

    [Fact]
    public async Task SealedLog_TailFrameRemoved_ReportsSealCountMismatch()
    {
        byte[] data = (await CreateEncryptedStreamAsync(_messages)).ToArray();
        V2Layout layout = ParseLayout(data);

        // Remove the last data frame but keep the seal — the attack the seal count detects.
        byte[] truncated = [.. data[..layout.Frames[^1].Offset], .. data[layout.SealOffset..]];

        (string text, DecryptionResult result) = await DecryptWithResultAsync(
            CreateMemoryStream(truncated)
        );

        text.ShouldBe(string.Concat(_messages[..^1]));
        result.Sessions.Count.ShouldBe(1);
        result.Sessions[0].SealStatus.ShouldBe(SealStatus.SealCountMismatch);
        result.Sessions[0].DeclaredFrameCount.ShouldBe((ulong)_messages.Length);
        result.Sessions[0].DecryptedMessages.ShouldBe(_messages.Length - 1);
        result.UnsealedSessions.ShouldBe(1);
    }

    [Fact]
    public async Task SealedLog_AllFramesRemoved_ReportsSealCountMismatch()
    {
        byte[] data = (await CreateEncryptedStreamAsync(_messages)).ToArray();
        V2Layout layout = ParseLayout(data);

        byte[] truncated = [.. data[..layout.HeaderLength], .. data[layout.SealOffset..]];

        (string text, DecryptionResult result) = await DecryptWithResultAsync(
            CreateMemoryStream(truncated)
        );

        text.ShouldBeEmpty();
        result.Sessions[0].SealStatus.ShouldBe(SealStatus.SealCountMismatch);
        result.Sessions[0].DeclaredFrameCount.ShouldBe((ulong)_messages.Length);
        result.Sessions[0].DecryptedMessages.ShouldBe(0);
    }

    [Fact]
    public async Task SealedLog_TailFrameRemoved_ThrowMode_Throws()
    {
        byte[] data = (await CreateEncryptedStreamAsync(_messages)).ToArray();
        V2Layout layout = ParseLayout(data);
        byte[] truncated = [.. data[..layout.Frames[^1].Offset], .. data[layout.SealOffset..]];

        CryptographicException exception = await Should.ThrowAsync<CryptographicException>(
            async () =>
                await DecryptWithResultAsync(CreateMemoryStream(truncated), ThrowOptions)
        );
        exception.Message.ShouldContain("truncated");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    [InlineData(24)]
    public async Task PartiallyWrittenSeal_ReportsUnsealed_WithoutFailures(int bytesDropped)
    {
        byte[] data = (await CreateEncryptedStreamAsync(_messages)).ToArray();

        (string text, DecryptionResult result) = await DecryptWithResultAsync(
            CreateMemoryStream(data[..^bytesDropped])
        );

        text.ShouldBe(string.Concat(_messages));
        result.FailedMessages.ShouldBe(0);
        result.Sessions[0].SealStatus.ShouldBe(SealStatus.Unsealed);
    }

    [Fact]
    public async Task TornSealMarker_ReportsUnsealed()
    {
        byte[] data = (await CreateEncryptedStreamAsync(_messages)).ToArray();

        // Keep only 2 of the 4 marker bytes — too short even for a length prefix.
        (string text, DecryptionResult result) = await DecryptWithResultAsync(
            CreateMemoryStream(data[..^26])
        );

        text.ShouldBe(string.Concat(_messages));
        result.Sessions[0].SealStatus.ShouldBe(SealStatus.Unsealed);
    }

    [Fact]
    public async Task PartialSealMarker_FollowedByAppendedSession_RecoversNextSession()
    {
        // Session A crashed exactly after writing the 4-byte seal marker,
        // then the application restarted and appended session B.
        byte[] sessionA = (await CreateEncryptedStreamAsync(_messages)).ToArray();
        byte[] sessionB = (await CreateEncryptedStreamAsync(["survivor message\n"])).ToArray();
        byte[] combined =
        [
            .. sessionA[..^EncryptionConstants.SealRecordRemainderLength],
            .. sessionB,
        ];

        (string text, DecryptionResult result) = await DecryptWithResultAsync(
            CreateMemoryStream(combined)
        );

        // All of A's frames and all of B decrypt; B is sealed, A is not verifiable.
        text.ShouldBe(string.Concat(_messages) + "survivor message\n");
        result.Sessions.Count.ShouldBe(2);
        result.Sessions[0].SealStatus.ShouldBeOneOf(SealStatus.Unsealed, SealStatus.SealInvalid);
        result.Sessions[1].SealStatus.ShouldBe(SealStatus.Sealed);
    }

    // ---------------------------------------------------------------------
    // Frame tampering: reorder / duplicate / delete
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ReorderedFrames_FailAuthentication()
    {
        // Same-length messages so the swap is structurally clean
        byte[] data = (await CreateEncryptedStreamAsync(["frame-aa\n", "frame-bb\n"])).ToArray();
        V2Layout layout = ParseLayout(data);
        byte[] tampered = (byte[])data.Clone();
        CopyFrame(data, layout.Frames[1], tampered, layout.Frames[0].Offset);
        CopyFrame(data, layout.Frames[0], tampered, layout.Frames[1].Offset);

        (string text, DecryptionResult result) = await DecryptWithResultAsync(
            CreateMemoryStream(tampered)
        );

        text.ShouldBeEmpty();
        result.FailedMessages.ShouldBeGreaterThanOrEqualTo(1);
        result.Sessions[0].SealStatus.ShouldBe(SealStatus.Unsealed);
    }

    [Fact]
    public async Task DuplicatedFrame_FailsAuthentication()
    {
        byte[] data = (await CreateEncryptedStreamAsync(_messages)).ToArray();
        V2Layout layout = ParseLayout(data);
        (int offset, int length) = layout.Frames[0];
        byte[] tampered =
        [
            .. data[..(offset + length)],
            .. data[offset..(offset + length)],
            .. data[(offset + length)..],
        ];

        (string text, DecryptionResult result) = await DecryptWithResultAsync(
            CreateMemoryStream(tampered)
        );

        // Only the first (authentic) copy decrypts; the replay fails.
        text.ShouldBe(_messages[0]);
        result.FailedMessages.ShouldBeGreaterThanOrEqualTo(1);
        result.Sessions[0].SealStatus.ShouldNotBe(SealStatus.Sealed);
    }

    [Fact]
    public async Task DeletedMiddleFrame_FailsAuthentication()
    {
        byte[] data = (await CreateEncryptedStreamAsync(_messages)).ToArray();
        V2Layout layout = ParseLayout(data);
        (int offset, int length) = layout.Frames[1];
        byte[] tampered = [.. data[..offset], .. data[(offset + length)..]];

        (string text, DecryptionResult result) = await DecryptWithResultAsync(
            CreateMemoryStream(tampered)
        );

        // Frame 0 decrypts; the frame that slid into position 1 fails (wrong seq + nonce).
        text.ShouldBe(_messages[0]);
        result.FailedMessages.ShouldBeGreaterThanOrEqualTo(1);
        result.Sessions[0].SealStatus.ShouldNotBe(SealStatus.Sealed);
    }

    [Fact]
    public async Task ReorderedFrames_ThrowMode_Throws()
    {
        byte[] data = (await CreateEncryptedStreamAsync(["frame-aa\n", "frame-bb\n"])).ToArray();
        V2Layout layout = ParseLayout(data);
        byte[] tampered = (byte[])data.Clone();
        CopyFrame(data, layout.Frames[1], tampered, layout.Frames[0].Offset);
        CopyFrame(data, layout.Frames[0], tampered, layout.Frames[1].Offset);

        await Should.ThrowAsync<CryptographicException>(async () =>
            await DecryptWithResultAsync(CreateMemoryStream(tampered), ThrowOptions)
        );
    }

    // ---------------------------------------------------------------------
    // Cross-session splicing
    // ---------------------------------------------------------------------

    [Fact]
    public async Task FrameSplicedFromAnotherSession_FailsAuthentication()
    {
        // Two sessions under the same key with identical structure
        byte[] sessionA = (await CreateEncryptedStreamAsync(["splice me\n"])).ToArray();
        byte[] sessionB = (await CreateEncryptedStreamAsync(["splice me\n"])).ToArray();
        V2Layout layoutA = ParseLayout(sessionA);
        V2Layout layoutB = ParseLayout(sessionB);

        byte[] tampered = (byte[])sessionA.Clone();
        CopyFrame(sessionB, layoutB.Frames[0], tampered, layoutA.Frames[0].Offset);

        (string text, DecryptionResult result) = await DecryptWithResultAsync(
            CreateMemoryStream(tampered)
        );

        text.ShouldBeEmpty();
        result.FailedMessages.ShouldBeGreaterThanOrEqualTo(1);
        result.Sessions[0].SealStatus.ShouldNotBe(SealStatus.Sealed);
    }

    [Fact]
    public async Task SealSplicedFromAnotherSession_ReportsSealInvalid()
    {
        byte[] sessionA = (await CreateEncryptedStreamAsync(["seal splice\n"])).ToArray();
        byte[] sessionB = (await CreateEncryptedStreamAsync(["seal splice\n"])).ToArray();
        V2Layout layoutA = ParseLayout(sessionA);
        V2Layout layoutB = ParseLayout(sessionB);

        byte[] tampered = [.. sessionA[..layoutA.SealOffset], .. sessionB[layoutB.SealOffset..]];

        (string text, DecryptionResult result) = await DecryptWithResultAsync(
            CreateMemoryStream(tampered)
        );

        text.ShouldBe("seal splice\n");
        result.Sessions[0].SealStatus.ShouldBe(SealStatus.SealInvalid);
        result.UnsealedSessions.ShouldBe(1);
    }

    // ---------------------------------------------------------------------
    // Records after the seal
    // ---------------------------------------------------------------------

    [Fact]
    public async Task DataFrameAfterSeal_ReportsSealInvalid()
    {
        byte[] data = (await CreateEncryptedStreamAsync(_messages)).ToArray();
        V2Layout layout = ParseLayout(data);
        (int offset, int length) = layout.Frames[0];
        byte[] tampered = [.. data, .. data[offset..(offset + length)]];

        (string text, DecryptionResult result) = await DecryptWithResultAsync(
            CreateMemoryStream(tampered)
        );

        text.ShouldBe(string.Concat(_messages));
        result.FailedMessages.ShouldBeGreaterThanOrEqualTo(1);
        result.Sessions[0].SealStatus.ShouldBe(SealStatus.SealInvalid);
    }

    [Fact]
    public async Task DuplicateSeal_ReportsSealInvalid()
    {
        byte[] data = (await CreateEncryptedStreamAsync(_messages)).ToArray();
        V2Layout layout = ParseLayout(data);
        byte[] tampered = [.. data, .. data[layout.SealOffset..]];

        (string text, DecryptionResult result) = await DecryptWithResultAsync(
            CreateMemoryStream(tampered)
        );

        text.ShouldBe(string.Concat(_messages));
        result.Sessions[0].SealStatus.ShouldBe(SealStatus.SealInvalid);
    }

    // ---------------------------------------------------------------------
    // Header tampering: version byte and keyId are now bound via the header hash
    // ---------------------------------------------------------------------

    [Fact]
    public async Task VersionByteTamperedDownToV1_NoPlaintextAccepted()
    {
        byte[] data = (await CreateEncryptedStreamAsync(_messages)).ToArray();
        byte[] tampered = (byte[])data.Clone();
        tampered[CryptographicUtils.MagicBytes.Length] = EncryptionConstants.FormatVersionV1;

        (string text, DecryptionResult result) = await DecryptWithResultAsync(
            CreateMemoryStream(tampered)
        );

        // Downgraded header parses as v1, but every frame was written with AAD,
        // so nothing decrypts without it.
        text.ShouldBeEmpty();
        result.DecryptedMessages.ShouldBe(0);
        result.FailedMessages.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task V1StreamTamperedUpToV2_NoPlaintextAccepted()
    {
        using MemoryStream v1Stream = new();
        using RSA rsa = RSA.Create();
        rsa.FromString(RsaKeyPair.publicKey);
        V1TestStreamBuilder.WriteSession(v1Stream, rsa, "", ["v1 upgrade tamper\n"]);
        byte[] tampered = v1Stream.ToArray();
        tampered[CryptographicUtils.MagicBytes.Length] = EncryptionConstants.FormatVersionV2;

        (string text, DecryptionResult result) = await DecryptWithResultAsync(
            CreateMemoryStream(tampered)
        );

        // Upgraded header demands AAD that v1 frames never carried.
        text.ShouldBeEmpty();
        result.DecryptedMessages.ShouldBe(0);
    }

    [Fact]
    public async Task KeyIdTampered_V2_FailsAuthentication()
    {
        // Both keyIds resolve to the same RSA private key, so in v1 this tamper
        // was undetectable; in v2 the keyId is bound via the header hash.
        using LocalKeyProvider keyProvider = new(
            new Dictionary<string, string>
            {
                { "key-aaaa", RsaKeyPair.privateKey },
                { "key-bbbb", RsaKeyPair.privateKey },
            }
        );
        DecryptionOptions options = new() { KeyProvider = keyProvider };

        byte[] data = (
            await CreateEncryptedStreamAsync(
                ["keyid tamper\n"],
                CreateEncryptionOptions(keyId: "key-aaaa")
            )
        ).ToArray();
        byte[] tampered = (byte[])data.Clone();
        Encoding
            .UTF8.GetBytes("key-bbbb")
            .CopyTo(tampered, CryptographicUtils.MagicBytes.Length + 1);

        (string text, DecryptionResult result) = await DecryptWithResultAsync(
            CreateMemoryStream(tampered),
            options
        );

        // The RSA header still decrypts (same private key) but every frame fails.
        text.ShouldBeEmpty();
        result.DecryptedMessages.ShouldBe(0);
        result.FailedMessages.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task KeyIdTampered_V1_WasUndetectable()
    {
        // Companion test documenting the v1 gap that the v2 header hash closes.
        using LocalKeyProvider keyProvider = new(
            new Dictionary<string, string>
            {
                { "key-aaaa", RsaKeyPair.privateKey },
                { "key-bbbb", RsaKeyPair.privateKey },
            }
        );
        DecryptionOptions options = new() { KeyProvider = keyProvider };

        using MemoryStream v1Stream = new();
        using RSA rsa = RSA.Create();
        rsa.FromString(RsaKeyPair.publicKey);
        V1TestStreamBuilder.WriteSession(v1Stream, rsa, "key-aaaa", ["v1 keyid tamper\n"]);
        byte[] tampered = v1Stream.ToArray();
        Encoding
            .UTF8.GetBytes("key-bbbb")
            .CopyTo(tampered, CryptographicUtils.MagicBytes.Length + 1);

        (string text, DecryptionResult result) = await DecryptWithResultAsync(
            CreateMemoryStream(tampered),
            options
        );

        // v1 has no header binding: the tampered keyId goes completely unnoticed.
        text.ShouldBe("v1 keyid tamper\n");
        result.FailedMessages.ShouldBe(0);
        result.Sessions[0].KeyId.ShouldBe("key-bbbb");
    }

    [Fact]
    public async Task UnsupportedVersion_SkipMode_NoSessions()
    {
        byte[] data = (await CreateEncryptedStreamAsync(_messages)).ToArray();
        byte[] tampered = (byte[])data.Clone();
        tampered[CryptographicUtils.MagicBytes.Length] = 3;

        (string text, DecryptionResult result) = await DecryptWithResultAsync(
            CreateMemoryStream(tampered)
        );

        text.ShouldBeEmpty();
        result.DecryptedSessions.ShouldBe(0);
        result.FailedHeaders.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task UnsupportedVersion_ThrowMode_Throws()
    {
        byte[] data = (await CreateEncryptedStreamAsync(_messages)).ToArray();
        byte[] tampered = (byte[])data.Clone();
        tampered[CryptographicUtils.MagicBytes.Length] = 3;

        await Should.ThrowAsync<NotSupportedException>(async () =>
            await DecryptWithResultAsync(CreateMemoryStream(tampered), ThrowOptions)
        );
    }

    // ---------------------------------------------------------------------
    // v1 backward compatibility
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("single-session", 1)]
    [InlineData("multi-session", 2)]
    [InlineData("with-keyid", 1)]
    public async Task V1Fixtures_DecryptByteExact_ReportNotApplicable(
        string fixtureName,
        int expectedSessions
    )
    {
        using LocalKeyProvider keyProvider = CreateFixtureKeyProvider();
        DecryptionOptions options = new()
        {
            KeyProvider = keyProvider,
            ErrorHandlingMode = ErrorHandlingMode.ThrowException,
        };

        await using FileStream input = System.IO.File.OpenRead(FixturePath($"{fixtureName}.log"));
        (string text, DecryptionResult result) = await DecryptWithResultAsync(input, options);

        string expected = await System.IO.File.ReadAllTextAsync(
            FixturePath($"{fixtureName}.expected.txt"),
            TestContext.Current.CancellationToken
        );
        text.ShouldBe(expected);
        result.Sessions.Count.ShouldBe(expectedSessions);
        result.Sessions.ShouldAllBe(s =>
            s.FormatVersion == EncryptionConstants.FormatVersionV1
            && s.SealStatus == SealStatus.NotApplicable
        );
        result.AllSessionsSealed.ShouldBeTrue();
        result.UnsealedSessions.ShouldBe(0);
    }

    [Fact]
    public async Task V1Fixture_RequireSealed_ThrowMode_Throws()
    {
        using LocalKeyProvider keyProvider = CreateFixtureKeyProvider();
        DecryptionOptions options = new()
        {
            KeyProvider = keyProvider,
            ErrorHandlingMode = ErrorHandlingMode.ThrowException,
            RequireSealed = true,
        };

        await using FileStream input = System.IO.File.OpenRead(FixturePath("single-session.log"));
        await Should.ThrowAsync<CryptographicException>(async () =>
            await DecryptWithResultAsync(input, options)
        );
    }

    [Fact]
    public async Task MixedV1AndV2Sessions_BothDecrypt_WithPerSessionStatus()
    {
        using MemoryStream v1Stream = new();
        using RSA rsa = RSA.Create();
        rsa.FromString(RsaKeyPair.publicKey);
        V1TestStreamBuilder.WriteSession(v1Stream, rsa, "", ["legacy v1 entry\n"]);

        byte[] v2Bytes = (await CreateEncryptedStreamAsync(["modern v2 entry\n"])).ToArray();
        byte[] combined = [.. v1Stream.ToArray(), .. v2Bytes];

        (string text, DecryptionResult result) = await DecryptWithResultAsync(
            CreateMemoryStream(combined)
        );

        text.ShouldBe("legacy v1 entry\nmodern v2 entry\n");
        result.Sessions.Count.ShouldBe(2);
        result.Sessions[0].FormatVersion.ShouldBe(EncryptionConstants.FormatVersionV1);
        result.Sessions[0].SealStatus.ShouldBe(SealStatus.NotApplicable);
        result.Sessions[1].FormatVersion.ShouldBe(EncryptionConstants.FormatVersionV2);
        result.Sessions[1].SealStatus.ShouldBe(SealStatus.Sealed);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private async Task<(string Text, DecryptionResult Result)> DecryptWithResultAsync(
        Stream input,
        DecryptionOptions? options = null
    )
    {
        using MemoryStream output = new();
        DecryptionResult result = await DecryptionUtils.DecryptLogFileAsync(
            input,
            output,
            options ?? DecryptOptions,
            cancellationToken: TestContext.Current.CancellationToken
        );
        return (Encoding.UTF8.GetString(output.ToArray()), result);
    }

    private static string FixturePath(string fileName) =>
        Path.Join(AppContext.BaseDirectory, "Fixtures", "v1", fileName);

    private static LocalKeyProvider CreateFixtureKeyProvider()
    {
        string privateKey = System.IO.File.ReadAllText(FixturePath("fixture-private-key.pem"));
        return new LocalKeyProvider(
            new Dictionary<string, string> { { "", privateKey }, { "fixture-key", privateKey } }
        );
    }

    private static void CopyFrame(
        byte[] source,
        (int Offset, int Length) frame,
        byte[] destination,
        int destinationOffset
    )
    {
        Array.Copy(source, frame.Offset, destination, destinationOffset, frame.Length);
    }

    /// <summary>
    /// Structural layout of a single-session v2 stream: header length, data frame ranges
    /// (offset includes the 4-byte length prefix), and the offset of the seal record.
    /// </summary>
    private sealed record V2Layout(
        int HeaderLength,
        List<(int Offset, int Length)> Frames,
        int SealOffset
    );

    private V2Layout ParseLayout(byte[] data)
    {
        using RSA rsa = RSA.Create();
        rsa.FromString(RsaKeyPair.publicKey);
        int headerLength =
            CryptographicUtils.MagicBytes.Length + 1 + HeaderMetadata.KeyIdLength + rsa.KeySize / 8;

        List<(int Offset, int Length)> frames = [];
        int position = headerLength;
        int sealOffset = -1;
        while (position + sizeof(int) <= data.Length)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(position));
            if (length == EncryptionConstants.SealMarkerDetection)
            {
                sealOffset = position;
                break;
            }
            frames.Add((position, sizeof(int) + length));
            position += sizeof(int) + length;
        }
        return new V2Layout(headerLength, frames, sealOffset);
    }
}
