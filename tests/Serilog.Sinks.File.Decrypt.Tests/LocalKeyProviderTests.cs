namespace Serilog.Sinks.File.Decrypt.Tests;

public class LocalKeyProviderTests
{
    private static readonly (string PublicKey, string PrivateKey) _xmlKeyPair =
        CryptographicUtils.GenerateRsaKeyPair(format: KeyFormat.Xml);

    private static readonly (string PublicKey, string PrivateKey) _pemKeyPair =
        CryptographicUtils.GenerateRsaKeyPair(format: KeyFormat.Pem);

    private static readonly (string PublicKey, string PrivateKey) _keyPair4096 =
        CryptographicUtils.GenerateRsaKeyPair(keySize: 4096, format: KeyFormat.Xml);

    private static readonly (string PublicKey, string PrivateKey) _encryptedPemKeyPair =
        CryptographicUtils.GenerateRsaKeyPair(2048, KeyFormat.Pem, "provider-passphrase");

    #region Passphrase-encrypted keys

    [Fact]
    public void Constructor_EncryptedKeyWithCorrectPassphrase_CreatesInstance()
    {
        Should.NotThrow(() =>
        {
            using var provider = new LocalKeyProvider(
                "key1",
                _encryptedPemKeyPair.PrivateKey,
                "provider-passphrase"
            );
        });
    }

    [Fact]
    public void Constructor_EncryptedKeyWithWrongPassphrase_ThrowsCryptographicException()
    {
        Should.Throw<CryptographicException>(() =>
            new LocalKeyProvider("key1", _encryptedPemKeyPair.PrivateKey, "wrong-passphrase")
        );
    }

    [Fact]
    public void Constructor_EncryptedKeyWithoutPassphrase_ThrowsCryptographicException()
    {
        Should
            .Throw<CryptographicException>(() =>
                new LocalKeyProvider("key1", _encryptedPemKeyPair.PrivateKey)
            )
            .Message.ShouldContain("passphrase");
    }

    [Fact]
    public void Constructor_KeyMapWithEncryptedKeys_SharesPassphrase()
    {
        var keyMap = new Dictionary<string, string>
        {
            { "enc", _encryptedPemKeyPair.PrivateKey },
            { "plain", _pemKeyPair.PrivateKey }, // unencrypted keys ignore the passphrase
        };

        Should.NotThrow(() =>
        {
            using var provider = new LocalKeyProvider(keyMap, "provider-passphrase");
        });
    }

    [Fact]
    public async Task DecryptAsync_WithEncryptedKey_DecryptsHeaderPayload()
    {
        // Arrange — RSA-encrypt a payload with the public key, decrypt via the provider
        using var rsa = RSA.Create();
        rsa.FromString(_encryptedPemKeyPair.PublicKey);
        byte[] payload = [1, 2, 3, 4];
        byte[] cipherText = rsa.Encrypt(payload, RSAEncryptionPadding.OaepSHA256);
        using var provider = new LocalKeyProvider(
            "",
            _encryptedPemKeyPair.PrivateKey,
            "provider-passphrase"
        );

        // Act
        byte[] decrypted = await provider.DecryptAsync(
            "",
            cipherText,
            TestContext.Current.CancellationToken
        );

        // Assert
        decrypted.ShouldBe(payload);
    }

    #endregion

    #region Constructor(Dictionary<string, string>)

    [Fact]
    public void Constructor_WithNullKeyMap_ThrowsArgumentNullException()
    {
        // Arrange
        Dictionary<string, string> keyMap = null!;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new LocalKeyProvider(keyMap));
    }

    [Fact]
    public void Constructor_WithEmptyKeyMap_ThrowsArgumentException()
    {
        // Arrange
        var keyMap = new Dictionary<string, string>();

        // Act & Assert
        Should.Throw<ArgumentException>(() => new LocalKeyProvider(keyMap));
    }

    [Fact]
    public void Constructor_WithValidXmlPrivateKey_CreatesInstanceSuccessfully()
    {
        // Arrange
        var keyMap = new Dictionary<string, string> { { "key1", _xmlKeyPair.PrivateKey } };

        // Act & Assert
        Should.NotThrow(() =>
        {
            using var provider = new LocalKeyProvider(keyMap);
        });
    }

    [Fact]
    public void Constructor_WithValidPemPrivateKey_CreatesInstanceSuccessfully()
    {
        // Arrange
        var keyMap = new Dictionary<string, string> { { "key1", _pemKeyPair.PrivateKey } };

        // Act & Assert
        Should.NotThrow(() =>
        {
            using var provider = new LocalKeyProvider(keyMap);
        });
    }

    [Fact]
    public void Constructor_WithMultipleValidKeys_CreatesInstanceSuccessfully()
    {
        // Arrange
        var keyMap = new Dictionary<string, string>
        {
            { "xml-key", _xmlKeyPair.PrivateKey },
            { "pem-key", _pemKeyPair.PrivateKey },
        };

        // Act & Assert
        Should.NotThrow(() =>
        {
            using var provider = new LocalKeyProvider(keyMap);
        });
    }

    [Fact]
    public void Constructor_WithInvalidKey_ThrowsCryptographicException()
    {
        // Arrange
        var keyMap = new Dictionary<string, string> { { "key1", "this-is-not-a-valid-rsa-key" } };

        // Act & Assert
        Should.Throw<CryptographicException>(() => new LocalKeyProvider(keyMap));
    }

    [Fact]
    public void Constructor_WithInvalidKey_ExceptionMessageContainsKeyId()
    {
        // Arrange
        const string KeyId = "my-key-id";
        var keyMap = new Dictionary<string, string> { { KeyId, "invalid-key-data" } };

        // Act & Assert
        Should
            .Throw<CryptographicException>(() => new LocalKeyProvider(keyMap))
            .Message.ShouldContain(KeyId);
    }

    [Fact]
    public void Constructor_WithOneInvalidKeyAmongMultiple_ThrowsCryptographicException()
    {
        // Arrange
        const string InvalidKeyId = "bad-key";
        var keyMap = new Dictionary<string, string>
        {
            { "good-key", _xmlKeyPair.PrivateKey },
            { InvalidKeyId, "not-a-valid-rsa-key" },
        };

        // Act & Assert
        Should
            .Throw<CryptographicException>(() => new LocalKeyProvider(keyMap))
            .Message.ShouldContain(InvalidKeyId);
    }

    #endregion

    #region Constructor(string, string)

    [Fact]
    public void Constructor_SingleKey_WithValidXmlPrivateKey_CreatesInstanceSuccessfully()
    {
        // Act & Assert
        Should.NotThrow(() =>
        {
            using var provider = new LocalKeyProvider("key1", _xmlKeyPair.PrivateKey);
        });
    }

    [Fact]
    public void Constructor_SingleKey_WithValidPemPrivateKey_CreatesInstanceSuccessfully()
    {
        // Act & Assert
        Should.NotThrow(() =>
        {
            using var provider = new LocalKeyProvider("key1", _pemKeyPair.PrivateKey);
        });
    }

    [Fact]
    public void Constructor_SingleKey_WithInvalidPrivateKey_ThrowsCryptographicException()
    {
        // Act & Assert
        Should.Throw<CryptographicException>(() =>
            new LocalKeyProvider("key1", "not-a-valid-rsa-key")
        );
    }

    [Fact]
    public void Constructor_SingleKey_WithInvalidKey_ExceptionMessageContainsKeyId()
    {
        // Arrange
        const string KeyId = "my-single-key";

        // Act & Assert
        Should
            .Throw<CryptographicException>(() => new LocalKeyProvider(KeyId, "invalid-key"))
            .Message.ShouldContain(KeyId);
    }

    #endregion

    #region DecryptAsync

    [Fact]
    public async Task DecryptAsync_WithValidKeyIdAndEncryptedData_ReturnsOriginalPlaintext()
    {
        // Arrange
        byte[] plaintext = "session-key-and-nonce-data"u8.ToArray();
        using var publicRsa = RSA.Create();
        publicRsa.FromString(_xmlKeyPair.PublicKey);
        byte[] cipherText = publicRsa.Encrypt(plaintext, RSAEncryptionPadding.OaepSHA256);

        using var provider = new LocalKeyProvider("key1", _xmlKeyPair.PrivateKey);

        // Act
        byte[] decrypted = await provider.DecryptAsync(
            "key1",
            cipherText,
            TestContext.Current.CancellationToken
        );

        // Assert
        decrypted.ShouldBe(plaintext);
    }

    [Fact]
    public async Task DecryptAsync_WithUnknownKeyId_ThrowsInvalidOperationException()
    {
        // Arrange
        using var provider = new LocalKeyProvider("key1", _xmlKeyPair.PrivateKey);
        byte[] dummyCipherText = new byte[256];

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await provider.DecryptAsync(
                "unknown-key-id",
                dummyCipherText,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task DecryptAsync_WithUnknownKeyId_ExceptionMessageContainsKeyId()
    {
        // Arrange
        const string UnknownKeyId = "unknown-key-id";
        using var provider = new LocalKeyProvider("key1", _xmlKeyPair.PrivateKey);
        byte[] dummyCipherText = new byte[256];

        // Act & Assert
        (
            await Should.ThrowAsync<InvalidOperationException>(async () =>
                await provider.DecryptAsync(
                    UnknownKeyId,
                    dummyCipherText,
                    TestContext.Current.CancellationToken
                )
            )
        ).Message.ShouldContain(UnknownKeyId);
    }

    [Fact]
    public async Task DecryptAsync_WithInvalidCipherText_ThrowsCryptographicException()
    {
        // Arrange
        using var provider = new LocalKeyProvider("key1", _xmlKeyPair.PrivateKey);
        byte[] invalidCipherText = RandomNumberGenerator.GetBytes(256);

        // Act & Assert
        await Should.ThrowAsync<CryptographicException>(async () =>
            await provider.DecryptAsync(
                "key1",
                invalidCipherText,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task DecryptAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        byte[] plaintext = "test-data"u8.ToArray();
        using var publicRsa = RSA.Create();
        publicRsa.FromString(_xmlKeyPair.PublicKey);
        byte[] cipherText = publicRsa.Encrypt(plaintext, RSAEncryptionPadding.OaepSHA256);

        using var provider = new LocalKeyProvider("key1", _xmlKeyPair.PrivateKey);
        using var cts = new CancellationTokenSource();

        // Act
        byte[] decrypted = await provider.DecryptAsync("key1", cipherText, cts.Token);

        // Assert
        decrypted.ShouldBe(plaintext);
    }

    [Fact]
    public async Task DecryptAsync_WithMultipleKeys_DecryptsUsingCorrectKey()
    {
        // Arrange
        byte[] plaintext = "multi-key-test"u8.ToArray();

        using var publicRsa2 = RSA.Create();
        publicRsa2.FromString(_pemKeyPair.PublicKey);
        byte[] cipherText = publicRsa2.Encrypt(plaintext, RSAEncryptionPadding.OaepSHA256);

        var keyMap = new Dictionary<string, string>
        {
            { "key1", _xmlKeyPair.PrivateKey },
            { "key2", _pemKeyPair.PrivateKey },
        };
        using var provider = new LocalKeyProvider(keyMap);

        // Act
        byte[] decrypted = await provider.DecryptAsync(
            "key2",
            cipherText,
            TestContext.Current.CancellationToken
        );

        // Assert
        decrypted.ShouldBe(plaintext);
    }

    #endregion

    #region GetKeySizeAsync

    [Fact]
    public async Task GetKeySizeAsync_WithDefault2048BitKey_Returns2048()
    {
        // Arrange
        using var provider = new LocalKeyProvider("key1", _xmlKeyPair.PrivateKey);

        // Act
        int keySize = await provider.GetKeySizeAsync("key1", TestContext.Current.CancellationToken);

        // Assert
        keySize.ShouldBe(2048);
    }

    [Fact]
    public async Task GetKeySizeAsync_With4096BitKey_Returns4096()
    {
        // Arrange
        using var provider = new LocalKeyProvider("key4096", _keyPair4096.PrivateKey);

        // Act
        int keySize = await provider.GetKeySizeAsync(
            "key4096",
            TestContext.Current.CancellationToken
        );

        // Assert
        keySize.ShouldBe(4096);
    }

    [Fact]
    public async Task GetKeySizeAsync_WithUnknownKeyId_ThrowsInvalidOperationException()
    {
        // Arrange
        using var provider = new LocalKeyProvider("key1", _xmlKeyPair.PrivateKey);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await provider.GetKeySizeAsync("unknown-key", TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task GetKeySizeAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        using var provider = new LocalKeyProvider("key1", _xmlKeyPair.PrivateKey);
        using var cts = new CancellationTokenSource();

        // Act
        int keySize = await provider.GetKeySizeAsync("key1", cts.Token);

        // Assert
        keySize.ShouldBe(2048);
    }

    [Fact]
    public async Task GetKeySizeAsync_WithMultipleKeys_ReturnsCorrectSizeForEachKey()
    {
        // Arrange
        var keyMap = new Dictionary<string, string>
        {
            { "key-2048", _xmlKeyPair.PrivateKey },
            { "key-4096", _keyPair4096.PrivateKey },
        };
        using var provider = new LocalKeyProvider(keyMap);

        // Act
        int size2048 = await provider.GetKeySizeAsync(
            "key-2048",
            TestContext.Current.CancellationToken
        );
        int size4096 = await provider.GetKeySizeAsync(
            "key-4096",
            TestContext.Current.CancellationToken
        );

        // Assert
        size2048.ShouldBe(2048);
        size4096.ShouldBe(4096);
    }

    #endregion

    #region IDisposable

    [Fact]
    public void Dispose_CanBeCalledWithoutException()
    {
        // Arrange
        var provider = new LocalKeyProvider("key1", _xmlKeyPair.PrivateKey);

        // Act & Assert
        Should.NotThrow(provider.Dispose);
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var provider = new LocalKeyProvider("key1", _xmlKeyPair.PrivateKey);

        // Act & Assert
        Should.NotThrow(() =>
        {
            provider.Dispose();
            provider.Dispose();
        });
    }

    #endregion
}
