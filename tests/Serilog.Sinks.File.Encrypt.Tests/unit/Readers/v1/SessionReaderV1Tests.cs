namespace Serilog.Sinks.File.Encrypt.Tests;

public sealed class SessionReaderV1Tests : IDisposable
{
    private readonly MemoryStream _input;
    private readonly SessionReaderV1 _sut;
    private readonly byte[] _aesKey;
    private readonly byte[] _nonce;
    private readonly RSA _encryptionRsa = RSA.Create();
    private const string KeyId = "test-key-id";
    private readonly Dictionary<string, RSA> _keyMap = [];
    private readonly DecryptionOptions _decOptions;

    public SessionReaderV1Tests()
    {
        (string publicKey, string privateKey) = CryptographicUtils.GenerateRsaKeyPair();
        _encryptionRsa.FromString(publicKey);
        _decOptions = TestUtils.GetDecryptionOptions(privateKey, KeyId);
        _input = new MemoryStream();
        _sut = new SessionReaderV1(new HeaderReaderV1());
        (_aesKey, _nonce) = TestUtils.CreateSessionData();
        BuildKeyMap();
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
            _keyMap,
            TestContext.Current.CancellationToken
        );

        // Assert
        context.SessionKey.ShouldBe(_aesKey);
        context.Nonce.ShouldBe(_nonce);
    }

    [Fact]
    public async Task GivenNoMatchingKey_WhenReadSessionAsync_ThenThrows()
    {
        // Arrange
        byte[] sessionData = new byte[_aesKey.Length + _nonce.Length];
        _aesKey.CopyTo(sessionData, 0);
        _nonce.CopyTo(sessionData, _aesKey.Length);
        byte[] validHeader = CreateHeader(sessionData);
        await _input.WriteAsync(validHeader, TestContext.Current.CancellationToken);
        _input.Seek(0, SeekOrigin.Begin);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await _sut.ReadSessionAsync(
                _input,
                new Dictionary<string, RSA>(), // empty key map to force failure
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task GivenHeaderShorterThanAesKey_WhenReadSessionAsync_ThenThrows()
    {
        // Arrange
        byte[] sessionData = new byte[HeaderMetadataV1.AesKeyLength - 1]; // intentionally too short
        _aesKey[..sessionData.Length].CopyTo(sessionData, 0);
        byte[] invalidHeader = CreateHeader(sessionData);
        await _input.WriteAsync(invalidHeader, TestContext.Current.CancellationToken);
        _input.Seek(0, SeekOrigin.Begin);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidDataException>(async () =>
            await _sut.ReadSessionAsync(_input, _keyMap, TestContext.Current.CancellationToken)
        );
        exception.Message.ShouldContain("Decrypted payload is too short to read AES key");
    }

    [Fact]
    public async Task GivenHeaderShorterThanNonce_WhenReadSessionAsync_ThenThrows()
    {
        // Arrange
        byte[] sessionData = new byte[
            HeaderMetadataV1.AesKeyLength + HeaderMetadataV1.NonceLength - 1
        ]; // intentionally too short for nonce
        _aesKey.CopyTo(sessionData, 0);
        // copy only part of the nonce to make it too short
        _nonce[..(sessionData.Length - HeaderMetadataV1.AesKeyLength)]
            .CopyTo(sessionData, HeaderMetadataV1.AesKeyLength);
        byte[] invalidHeader = CreateHeader(sessionData);
        await _input.WriteAsync(invalidHeader, TestContext.Current.CancellationToken);
        _input.Seek(0, SeekOrigin.Begin);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidDataException>(async () =>
            await _sut.ReadSessionAsync(_input, _keyMap, TestContext.Current.CancellationToken)
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
        for (int i = HeaderMetadataV1.KeyIdLength; i < validHeader.Length; i++)
        {
            validHeader[i] ^= 0xFF; // flip bits to corrupt the data
        }
        await _input.WriteAsync(validHeader, TestContext.Current.CancellationToken);
        _input.Seek(0, SeekOrigin.Begin);

        // Act & Assert
        var exception = await Should.ThrowAsync<CryptographicException>(async () =>
            await _sut.ReadSessionAsync(_input, _keyMap, TestContext.Current.CancellationToken)
        );
        exception.Message.ShouldContain("RSA decryption of header failed");
    }

    private byte[] CreateHeader(byte[] sessionData)
    {
        // write the plaintext key ID 32 bytes padded with zeros
        // then add the encrypted session key and nonce (for simplicity, we just concatenate them here)
        byte[] header = new byte[HeaderMetadataV1.KeyIdLength + _encryptionRsa.KeySize / 8];
        // padded with 0s to ensure fixed length
        byte[] keyIdBytes = new byte[HeaderMetadataV1.KeyIdLength];
        byte[] rawKeyIdBytes = Encoding.UTF8.GetBytes(KeyId);
        Array.Copy(rawKeyIdBytes, keyIdBytes, Math.Min(rawKeyIdBytes.Length, keyIdBytes.Length));
        ReadOnlySpan<byte> session = _encryptionRsa.Encrypt(
            sessionData,
            RSAEncryptionPadding.OaepSHA256
        );
        Array.Copy(keyIdBytes, 0, header, 0, HeaderMetadataV1.KeyIdLength);
        Array.Copy(session.ToArray(), 0, header, HeaderMetadataV1.KeyIdLength, session.Length);
        return header;
    }

    private void BuildKeyMap()
    {
        foreach (KeyValuePair<string, string> kvp in _decOptions.DecryptionKeys)
        {
            var rsa = RSA.Create();
            rsa.FromString(kvp.Value);
            _keyMap[kvp.Key] = rsa;
        }
    }

    public void Dispose()
    {
        _input.Dispose();
        foreach (RSA rsa in _keyMap.Values)
        {
            rsa.Dispose();
        }
        _encryptionRsa.Dispose();
    }
}
