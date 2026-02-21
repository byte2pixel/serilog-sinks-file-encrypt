using Serilog.Sinks.File.Encrypt.Models;
using Serilog.Sinks.File.Encrypt.Readers.v1;
using Serilog.Sinks.File.Encrypt.Writers.v1;

namespace Serilog.Sinks.File.Encrypt.Tests.unit.v1;

public sealed class SessionReaderV1Tests : IDisposable
{
    private readonly MemoryStream _input;
    private readonly SessionReaderV1 _sut;
    private readonly byte[] _aesKey;
    private readonly byte[] _nonce;
    private readonly Dictionary<string, RSA> _keyMap = [];
    private readonly EncryptionOptions _encOptions;
    private readonly DecryptionOptions _decOptions;

    public SessionReaderV1Tests()
    {
        (string publicKey, string privateKey) = CryptographicUtils.GenerateRsaKeyPair();
        _encOptions = TestUtils.GetEncryptionOptions(publicKey, "test-key-id");
        _decOptions = TestUtils.GetDecryptionOptions(privateKey, "test-key-id");
        _input = new MemoryStream();
        _sut = new SessionReaderV1(new HeaderReaderV1());
        (_aesKey, _nonce) = TestUtils.CreateSessionData();
        BuildKeyMap();
    }

    [Fact]
    public async Task GivenValidHeader_WhenReadSessionAsync_ThenReturnsSession()
    {
        // Arrange
        byte[] validHeader = CreateValidHeader();
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
        byte[] validHeader = CreateValidHeader();
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

    private byte[] CreateValidHeader()
    {
        // write the plaintext key ID 32 bytes padded with zeros
        // then add the encrypted session key and nonce (for simplicity, we just concatenate them here)
        var encryptor = new HeaderWriterV1(_encOptions);
        byte[] header = new byte[HeaderMetadataV1.KeyIdLength + _encOptions.Rsa.KeySize / 8];
        // padded with 0s to ensure fixed length
        byte[] keyIdBytes = new byte[HeaderMetadataV1.KeyIdLength];
        byte[] rawKeyIdBytes = Encoding.UTF8.GetBytes(_encOptions.KeyId);
        Array.Copy(rawKeyIdBytes, keyIdBytes, Math.Min(rawKeyIdBytes.Length, keyIdBytes.Length));

        ReadOnlySpan<byte> session = encryptor.Encrypt(_aesKey, _nonce);
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
        GC.SuppressFinalize(this);
    }
}
