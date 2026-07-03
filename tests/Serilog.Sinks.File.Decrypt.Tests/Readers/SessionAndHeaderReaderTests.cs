using System.Buffers.Binary;

namespace Serilog.Sinks.File.Decrypt.Tests;

public sealed class SessionAndHeaderReaderTests : IDisposable
{
    private readonly MemoryStream _input;
    private readonly SessionReader _sut;
    private readonly byte[] _aesKey;
    private readonly byte[] _nonce;
    private readonly RSA _encryptionRsa = RSA.Create();
    private const string KeyId = "test-key-id";
    private readonly LocalKeyProvider _keyProvider;

    public SessionAndHeaderReaderTests()
    {
        (string publicKey, string privateKey) = CryptographicUtils.GenerateRsaKeyPair();
        _encryptionRsa.FromString(publicKey);
        _input = new MemoryStream();
        _sut = new SessionReader(new HeaderReader());
        _keyProvider = new LocalKeyProvider(KeyId, privateKey);
        (_aesKey, _nonce) = TestUtils.CreateSessionData();
    }

    [Fact]
    public async Task GivenValidHeader_WhenReadSessionAsync_ThenReturnsSession()
    {
        // Arrange
        byte[] sessionData = new byte[_aesKey.Length + _nonce.Length];
        _aesKey.CopyTo(sessionData, 0);
        _nonce.CopyTo(sessionData, _aesKey.Length);
        byte[] validHeader = CreateHeader(sessionData);
        await _input.WriteAsync(validHeader, TestContext.Current.CancellationToken);
        _input.Seek(0, SeekOrigin.Begin);

        // Act
        DecryptionContext context = await _sut.ReadSessionAsync(
            _input,
            _keyProvider,
            EncryptionConstants.FormatVersionV2,
            TestContext.Current.CancellationToken
        );

        // Assert
        context.SessionKey.ShouldBe(_aesKey);
        context.Nonce.ShouldBe(_nonce);
        context.Version.ShouldBe(EncryptionConstants.FormatVersionV2);
        context.KeyId.ShouldBe(KeyId);

        // The v2 header hash covers the exact on-disk header bytes:
        // magic + version + keyId + RSA payload. This test's stream omits magic/version
        // (LogReader consumes those), so reconstruct them for the expectation.
        byte[] rawHeader =
        [
            .. CryptographicUtils.MagicBytes,
            EncryptionConstants.FormatVersionV2,
            .. _input.ToArray(),
        ];
        context.HeaderHash.ShouldBe(SHA256.HashData(rawHeader));

        // The seal nonce is the initial nonce with its 64-bit little-endian counter decremented.
        byte[] expectedSealNonce = (byte[])_nonce.Clone();
        long counter = BinaryPrimitives.ReadInt64LittleEndian(expectedSealNonce.AsSpan(4));
        BinaryPrimitives.WriteInt64LittleEndian(expectedSealNonce.AsSpan(4), counter - 1);
        context.SealNonce.ShouldBe(expectedSealNonce);
    }

    [Fact]
    public async Task GivenValidHeader_WhenReadSessionAsyncAsV1_ThenNoVerificationState()
    {
        // Arrange
        byte[] sessionData = new byte[_aesKey.Length + _nonce.Length];
        _aesKey.CopyTo(sessionData, 0);
        _nonce.CopyTo(sessionData, _aesKey.Length);
        byte[] validHeader = CreateHeader(sessionData);
        await _input.WriteAsync(validHeader, TestContext.Current.CancellationToken);
        _input.Seek(0, SeekOrigin.Begin);

        // Act
        DecryptionContext context = await _sut.ReadSessionAsync(
            _input,
            _keyProvider,
            EncryptionConstants.FormatVersionV1,
            TestContext.Current.CancellationToken
        );

        // Assert
        context.SessionKey.ShouldBe(_aesKey);
        context.Nonce.ShouldBe(_nonce);
        context.Version.ShouldBe(EncryptionConstants.FormatVersionV1);
        context.HeaderHash.ShouldBeEmpty();
        context.SealNonce.ShouldBeEmpty();
    }

    [Fact]
    public async Task GivenNullKeyProvider_WhenReadSessionAsync_ThenThrows()
    {
        // Arrange
        byte[] sessionData = new byte[_aesKey.Length + _nonce.Length];
        _aesKey.CopyTo(sessionData, 0);
        _nonce.CopyTo(sessionData, _aesKey.Length);
        byte[] validHeader = CreateHeader(sessionData);
        await _input.WriteAsync(validHeader, TestContext.Current.CancellationToken);
        _input.Seek(0, SeekOrigin.Begin);

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await _sut.ReadSessionAsync(
                _input,
                null!,
                EncryptionConstants.FormatVersionV2,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task GivenHeaderShorterThanAesKey_WhenReadSessionAsync_ThenThrows()
    {
        // Arrange
        byte[] sessionData = new byte[HeaderMetadata.AesKeyLength - 1]; // intentionally too short
        _aesKey[..sessionData.Length].CopyTo(sessionData, 0);
        byte[] invalidHeader = CreateHeader(sessionData);
        await _input.WriteAsync(invalidHeader, TestContext.Current.CancellationToken);
        _input.Seek(0, SeekOrigin.Begin);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidDataException>(async () =>
            await _sut.ReadSessionAsync(
                _input,
                _keyProvider,
                EncryptionConstants.FormatVersionV2,
                TestContext.Current.CancellationToken
            )
        );
        exception.Message.ShouldContain("Decrypted payload is too short to read AES key");
    }

    [Fact]
    public async Task GivenHeaderShorterThanNonce_WhenReadSessionAsync_ThenThrows()
    {
        // Arrange
        byte[] sessionData = new byte[HeaderMetadata.AesKeyLength + HeaderMetadata.NonceLength - 1]; // intentionally too short for nonce
        _aesKey.CopyTo(sessionData, 0);
        // copy only part of the nonce to make it too short
        _nonce[..(sessionData.Length - HeaderMetadata.AesKeyLength)]
            .CopyTo(sessionData, HeaderMetadata.AesKeyLength);
        byte[] invalidHeader = CreateHeader(sessionData);
        await _input.WriteAsync(invalidHeader, TestContext.Current.CancellationToken);
        _input.Seek(0, SeekOrigin.Begin);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidDataException>(async () =>
            await _sut.ReadSessionAsync(
                _input,
                _keyProvider,
                EncryptionConstants.FormatVersionV2,
                TestContext.Current.CancellationToken
            )
        );
        exception.Message.ShouldContain("Decrypted payload is too short to read the nonce");
    }

    [Fact]
    public async Task GivenInvalidRsaPayload_WhenReadSessionAsync_ThenThrows()
    {
        // Arrange
        byte[] sessionData = new byte[_aesKey.Length + _nonce.Length];
        _aesKey.CopyTo(sessionData, 0);
        _nonce.CopyTo(sessionData, _aesKey.Length);
        byte[] validHeader = CreateHeader(sessionData);
        // corrupt the RSA payload by changing some bytes in the header after the keyId
        for (int i = HeaderMetadata.KeyIdLength; i < validHeader.Length; i++)
        {
            validHeader[i] ^= 0xFF; // flip bits to corrupt the data
        }
        await _input.WriteAsync(validHeader, TestContext.Current.CancellationToken);
        _input.Seek(0, SeekOrigin.Begin);

        // Act & Assert
        var exception = await Should.ThrowAsync<CryptographicException>(async () =>
            await _sut.ReadSessionAsync(
                _input,
                _keyProvider,
                EncryptionConstants.FormatVersionV2,
                TestContext.Current.CancellationToken
            )
        );
        exception.Message.ShouldContain("RSA decryption of header failed");
    }

    private byte[] CreateHeader(byte[] sessionData)
    {
        byte[] header = new byte[HeaderMetadata.KeyIdLength + _encryptionRsa.KeySize / 8];
        byte[] keyIdBytes = new byte[HeaderMetadata.KeyIdLength];
        byte[] rawKeyIdBytes = Encoding.UTF8.GetBytes(KeyId);
        Array.Copy(rawKeyIdBytes, keyIdBytes, Math.Min(rawKeyIdBytes.Length, keyIdBytes.Length));
        ReadOnlySpan<byte> session = _encryptionRsa.Encrypt(
            sessionData,
            RSAEncryptionPadding.OaepSHA256
        );
        Array.Copy(keyIdBytes, 0, header, 0, HeaderMetadata.KeyIdLength);
        Array.Copy(session.ToArray(), 0, header, HeaderMetadata.KeyIdLength, session.Length);
        return header;
    }

    public void Dispose()
    {
        _input.Dispose();
        _encryptionRsa.Dispose();
        _keyProvider.Dispose();
    }
}
