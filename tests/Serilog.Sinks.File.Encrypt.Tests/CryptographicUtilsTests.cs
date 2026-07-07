namespace Serilog.Sinks.File.Encrypt.Tests;

public class CryptographicUtilsTests : EncryptionTestBase
{
    [Fact]
    public void IncreaseNonce_IncrementsCounterAsLittleEndian()
    {
        // Arrange - 12-byte nonce; the first 4 bytes are fixed, the last 8 are the counter.
        byte[] nonce = new byte[EncryptionConstants.NonceLength];

        // Act
        nonce.IncreaseNonce();

        // Assert - counter == 1 stored little-endian in the last 8 bytes, regardless of host endianness.
        byte[] expected = [0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0];
        nonce.ShouldBe(expected);
    }

    [Fact]
    public void InitializeRsa_FromXml_ReturnsInitializedRsaInstance()
    {
        // Arrange, Act
        (string publicKey, string privateKey) = CryptographicUtils.GenerateRsaKeyPair(
            format: KeyFormat.Xml
        );

        // Assert
        Should.NotThrow(() =>
        {
            using var privateKeyRsa = RSA.Create();
            privateKeyRsa.FromString(privateKey);
        });

        Should.NotThrow(() =>
        {
            using var publicKeyRsa = RSA.Create();
            publicKeyRsa.FromString(publicKey);
        });
    }

    [Fact]
    public void InitializeRsa_FromPem_ReturnsInitializedRsaInstance()
    {
        // Arrange, Act
        (string publicKey, string privateKey) = CryptographicUtils.GenerateRsaKeyPair(
            format: KeyFormat.Pem
        );

        Should.NotThrow(() =>
        {
            // Assert
            using var privateKeyRsa = RSA.Create();
            privateKeyRsa.FromString(privateKey);
        });

        Should.NotThrow(() =>
        {
            using var publicKeyRsa = RSA.Create();
            publicKeyRsa.FromString(publicKey);
        });
    }

    [Fact]
    public void GenerateRsaKeyPair_DefaultFormat_IsPem()
    {
        // Arrange, Act — 6.0.0 flipped the default from Xml to Pem
        (string publicKey, string privateKey) = CryptographicUtils.GenerateRsaKeyPair();

        // Assert
        publicKey.ShouldStartWith("-----BEGIN RSA PUBLIC KEY-----");
        privateKey.ShouldStartWith("-----BEGIN RSA PRIVATE KEY-----");
    }

    [Fact]
    public void GenerateRsaKeyPair_WithPassphrase_ProducesEncryptedPkcs8ThatRoundTrips()
    {
        // Arrange
        const string Passphrase = "correct horse battery staple";

        // Act
        (string publicKey, string privateKey) = CryptographicUtils.GenerateRsaKeyPair(
            2048,
            KeyFormat.Pem,
            Passphrase
        );

        // Assert — encrypted PKCS#8 private key, plaintext public key
        privateKey.ShouldStartWith("-----BEGIN ENCRYPTED PRIVATE KEY-----");
        publicKey.ShouldStartWith("-----BEGIN RSA PUBLIC KEY-----");

        using var rsa = RSA.Create();
        Should.NotThrow(() => rsa.ImportFromEncryptedPem(privateKey, Passphrase));

        // The imported key pair must actually match the exported public key
        using var publicRsa = RSA.Create();
        publicRsa.FromString(publicKey);
        byte[] cipher = publicRsa.Encrypt([1, 2, 3], RSAEncryptionPadding.OaepSHA256);
        rsa.Decrypt(cipher, RSAEncryptionPadding.OaepSHA256).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public void GenerateRsaKeyPair_WithPassphrase_WrongPassphraseFailsImport()
    {
        // Arrange
        (_, string privateKey) = CryptographicUtils.GenerateRsaKeyPair(
            2048,
            KeyFormat.Pem,
            "right passphrase"
        );

        // Act & Assert
        using var rsa = RSA.Create();
        Should.Throw<CryptographicException>(() =>
            rsa.ImportFromEncryptedPem(privateKey, "wrong passphrase")
        );
    }

    [Fact]
    public void GenerateRsaKeyPair_XmlWithPassphrase_ThrowsNotSupported()
    {
        Should
            .Throw<NotSupportedException>(() =>
                CryptographicUtils.GenerateRsaKeyPair(2048, KeyFormat.Xml, "passphrase")
            )
            .Message.ShouldContain("Pem");
    }

    [Fact]
    public void GenerateRsaKeyPair_EmptyPassphrase_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() =>
            CryptographicUtils.GenerateRsaKeyPair(2048, KeyFormat.Pem, ReadOnlySpan<char>.Empty)
        );
    }

    [Fact]
    public void GenerateRsaKeyPair_WithPassphrase_EnforcesMinimumKeySize()
    {
        Should.Throw<CryptographicException>(() =>
            CryptographicUtils.GenerateRsaKeyPair(1024, KeyFormat.Pem, "passphrase")
        );
    }

    [Fact]
    public void FromString_EncryptedPemWithoutPassphrase_ThrowsWithClearMessage()
    {
        (_, string encryptedKey) = CryptographicUtils.GenerateRsaKeyPair(
            2048,
            KeyFormat.Pem,
            "passphrase"
        );
        using var rsa = RSA.Create();

        Should
            .Throw<CryptographicException>(() => rsa.FromString(encryptedKey))
            .Message.ShouldContain("passphrase-encrypted");
    }

    [Fact]
    public void FromString_EncryptedPemWithPassphrase_Imports()
    {
        (_, string encryptedKey) = CryptographicUtils.GenerateRsaKeyPair(
            2048,
            KeyFormat.Pem,
            "passphrase"
        );
        using var rsa = RSA.Create();

        Should.NotThrow(() => rsa.FromString(encryptedKey, "passphrase"));
        rsa.KeySize.ShouldBe(2048);
    }

    [Fact]
    public void FromString_EncryptedPemWithWrongPassphrase_Throws()
    {
        (_, string encryptedKey) = CryptographicUtils.GenerateRsaKeyPair(
            2048,
            KeyFormat.Pem,
            "right"
        );
        using var rsa = RSA.Create();

        Should.Throw<CryptographicException>(() => rsa.FromString(encryptedKey, "wrong"));
    }

    [Fact]
    public void FromString_EncryptedPemWithEmptyPassphrase_ThrowsWithClearMessage()
    {
        (_, string encryptedKey) = CryptographicUtils.GenerateRsaKeyPair(
            2048,
            KeyFormat.Pem,
            "passphrase"
        );
        using var rsa = RSA.Create();

        Should
            .Throw<CryptographicException>(() =>
                rsa.FromString(encryptedKey, ReadOnlySpan<char>.Empty)
            )
            .Message.ShouldContain("passphrase-encrypted");
    }

    [Fact]
    public void FromString_PassphraseOverloadWithUnencryptedKey_IgnoresPassphrase()
    {
        (_, string plainKey) = CryptographicUtils.GenerateRsaKeyPair(format: KeyFormat.Pem);
        using var rsa = RSA.Create();

        Should.NotThrow(() => rsa.FromString(plainKey, "irrelevant"));
    }

    [Fact]
    public void IsEncryptedPem_DistinguishesKeyKinds()
    {
        (_, string encryptedKey) = CryptographicUtils.GenerateRsaKeyPair(
            2048,
            KeyFormat.Pem,
            "passphrase"
        );
        (_, string plainPem) = CryptographicUtils.GenerateRsaKeyPair(format: KeyFormat.Pem);
        (_, string plainXml) = CryptographicUtils.GenerateRsaKeyPair(format: KeyFormat.Xml);

        CryptographicUtils.IsEncryptedPem(encryptedKey).ShouldBeTrue();
        CryptographicUtils.IsEncryptedPem(plainPem).ShouldBeFalse();
        CryptographicUtils.IsEncryptedPem(plainXml).ShouldBeFalse();
    }

    [Fact]
    public void InitializeRsa_FromUnknown_ThrowsCryptographicException()
    {
        // Arrange, Act
        const string UnknownFormat = "UNKOWN_FORMAT";

        // Assert
        Should.Throw<CryptographicException>(() =>
        {
            using var rsa = RSA.Create();
            rsa.FromString(UnknownFormat);
        });
    }

    [Fact]
    public void InitializeRsa_FromInvalidXml_ThrowsFormatException()
    {
        // Arrange, Act
        const string InvalidXml =
            "<RSAKeyValue><Modulus>...</Modulus><Exponent>...</Exponent></RSAKeyValue>";

        // Assert
        Should.Throw<CryptographicException>(() =>
        {
            using var rsa = RSA.Create();
            rsa.FromString(InvalidXml);
        });
    }

    [Fact]
    public void InitializeRsa_FromInvalidPem_ThrowsArgumentException()
    {
        // Arrange, Act
        const string InvalidPem =
            "-----BEGIN RSA PUBLIC KEY-----\nMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA7\n-----END RSA PUBLIC KEY-----";

        // Assert
        Should.Throw<CryptographicException>(() =>
        {
            using var rsa = RSA.Create();
            rsa.FromString(InvalidPem);
        });
    }

    [Fact]
    public void GenerateRsaKeyPair_WithXmlFormat_ReturnsValidKeys()
    {
        // Arrange, Act
        (string publicKey, string privateKey) = CryptographicUtils.GenerateRsaKeyPair(
            format: KeyFormat.Xml
        );

        using var privateKeyRsa = RSA.Create();
        privateKeyRsa.FromXmlString(privateKey);

        using var publicKeyRsa = RSA.Create();
        publicKeyRsa.FromXmlString(publicKey);

        byte[] data = "ABCD"u8.ToArray();
        byte[] encryptedData = publicKeyRsa.Encrypt(data, RSAEncryptionPadding.Pkcs1);
        byte[] decryptedData = privateKeyRsa.Decrypt(encryptedData, RSAEncryptionPadding.Pkcs1);

        // Assert
        decryptedData.ShouldBe(data);
    }

    [Fact]
    public void GenerateRsaKeyPair_WithPemFormat_ReturnsValidKeys()
    {
        // Arrange, Act
        (string publicKey, string privateKey) = CryptographicUtils.GenerateRsaKeyPair(
            format: KeyFormat.Pem
        );

        using var privateKeyRsa = RSA.Create();
        privateKeyRsa.ImportFromPem(privateKey);

        using var publicKeyRsa = RSA.Create();
        publicKeyRsa.ImportFromPem(publicKey);

        byte[] data = "ABCD"u8.ToArray();
        byte[] encryptedData = publicKeyRsa.Encrypt(data, RSAEncryptionPadding.Pkcs1);
        byte[] decryptedData = privateKeyRsa.Decrypt(encryptedData, RSAEncryptionPadding.Pkcs1);

        // Assert
        decryptedData.ShouldBe(data);
    }

    [Fact]
    public void GenerateRsaKeyPair_With4096BitKey_WithXmlFormat_ReturnsValidKeys()
    {
        // Arrange & Act
        (string publicKey, string privateKey) = CryptographicUtils.GenerateRsaKeyPair(
            keySize: 4096,
            format: KeyFormat.Xml
        );

        using var privateKeyRsa = RSA.Create();
        privateKeyRsa.FromXmlString(privateKey);

        using var publicKeyRsa = RSA.Create();
        publicKeyRsa.FromXmlString(publicKey);

        // Assert - Verify key size is 4096 bits
        privateKeyRsa.KeySize.ShouldBe(4096);
        publicKeyRsa.KeySize.ShouldBe(4096);

        // Verify keys can encrypt and decrypt
        byte[] data = "Test data for 4096-bit key"u8.ToArray();
        byte[] encryptedData = publicKeyRsa.Encrypt(data, RSAEncryptionPadding.Pkcs1);
        byte[] decryptedData = privateKeyRsa.Decrypt(encryptedData, RSAEncryptionPadding.Pkcs1);

        decryptedData.ShouldBe(data);
    }

    [Fact]
    public void GenerateRsaKeyPair_With4096BitKey_WithPemFormat_ReturnsValidKeys()
    {
        // Arrange & Act
        (string publicKey, string privateKey) = CryptographicUtils.GenerateRsaKeyPair(
            keySize: 4096,
            format: KeyFormat.Pem
        );

        using var privateKeyRsa = RSA.Create();
        privateKeyRsa.ImportFromPem(privateKey);

        using var publicKeyRsa = RSA.Create();
        publicKeyRsa.ImportFromPem(publicKey);

        // Assert - Verify key size is 4096 bits
        privateKeyRsa.KeySize.ShouldBe(4096);
        publicKeyRsa.KeySize.ShouldBe(4096);

        // Verify keys can encrypt and decrypt
        byte[] data = "Test data for 4096-bit key"u8.ToArray();
        byte[] encryptedData = publicKeyRsa.Encrypt(data, RSAEncryptionPadding.Pkcs1);
        byte[] decryptedData = privateKeyRsa.Decrypt(encryptedData, RSAEncryptionPadding.Pkcs1);

        decryptedData.ShouldBe(data);
    }

    [Theory]
    [InlineData(1024)]
    [InlineData(2047)]
    [InlineData(512)]
    public void GenerateRsaKeyPair_WithKeySizeBelowMinimum_ThrowsCryptographicException(int keySize)
    {
        // Act & Assert
        Should
            .Throw<CryptographicException>(() =>
                CryptographicUtils.GenerateRsaKeyPair(keySize: keySize)
            )
            .Message.ShouldContain("at least");
    }

    [Fact]
    public void GenerateRsaKeyPair_WithMinimumKeySize_ReturnsValidKeys()
    {
        // Arrange & Act
        (string publicKey, string privateKey) = CryptographicUtils.GenerateRsaKeyPair(
            keySize: EncryptionConstants.MinimumRsaKeySize
        );

        using var privateKeyRsa = RSA.Create();
        privateKeyRsa.FromString(privateKey);

        using var publicKeyRsa = RSA.Create();
        publicKeyRsa.FromString(publicKey);

        // Assert
        privateKeyRsa.KeySize.ShouldBe(EncryptionConstants.MinimumRsaKeySize);
        publicKeyRsa.KeySize.ShouldBe(EncryptionConstants.MinimumRsaKeySize);
    }

    [Fact]
    public void GenerateRsaKeyPair_WithInvalidFormat_ThrowsArgumentException()
    {
        // Act & Assert
        Should
            .Throw<NotSupportedException>(() =>
                CryptographicUtils.GenerateRsaKeyPair(format: (KeyFormat)999)
            )
            .Message.ShouldContain("Unsupported key format: 999");
    }

    [Fact]
    public async Task DecryptLogFileAsync_ReturnsDecryptedContent()
    {
        // Arrange
        const string OriginalText = "Hello, simple encrypted log!";

        // Act - Use helper method that handles all stream management
        string decrypted = await EncryptAndDecryptAsync(OriginalText);

        // Assert
        decrypted.ShouldContain(OriginalText);
    }

    [Fact]
    public async Task DecryptLogFileAsync_ReturnsMultipleDecryptedEntries()
    {
        // Arrange
        const string LogMessage1 = "Simple log file test!";
        const string LogMessage2 = "Second simple log entry!";

        // Act - Use helper method that handles all stream management
        string decrypted = await EncryptAndDecryptAsync([LogMessage1, LogMessage2]);

        // Assert
        decrypted.ShouldContain(LogMessage1);
        decrypted.ShouldContain(LogMessage2);
    }

    [Fact]
    public async Task EncryptedStream_With4096BitKey_EncryptsAndDecryptsSuccessfully()
    {
        // Arrange
        (string publicKey, string privateKey) = CryptographicUtils.GenerateRsaKeyPair(
            keySize: 4096
        );
        const string OriginalText = "Testing 4096-bit RSA key with encrypted stream!";

        using var rsa = RSA.Create();
        rsa.FromString(publicKey);
        EncryptionOptions options = new(rsa, "4096");
        LocalKeyProvider keyProvider = new("4096", privateKey);
        DecryptionOptions decryptionOptions = new() { KeyProvider = keyProvider };

        // Act - Use helper that manages streams automatically
        string decrypted = await EncryptAndDecryptAsync(OriginalText, options, decryptionOptions);

        // Assert
        decrypted.ShouldBe(OriginalText);
    }

    [Fact]
    public async Task EncryptedStream_With4096BitKey_HandlesMultipleChunks()
    {
        // Arrange
        (string publicKey, string privateKey) = CryptographicUtils.GenerateRsaKeyPair(
            keySize: 4096
        );
        using var rsa = RSA.Create();
        rsa.FromString(publicKey);
        EncryptionOptions options = new(rsa, "4096");
        LocalKeyProvider keyProvider = new("4096", privateKey);
        DecryptionOptions decryptionOptions = new() { KeyProvider = keyProvider };
        const string LogMessage1 = "First log entry with 4096-bit key";
        const string LogMessage2 = "Second log entry with 4096-bit key";
        const string LogMessage3 = "Third log entry with 4096-bit key";

        // Act - Use helper that manages streams automatically
        string decrypted = await EncryptAndDecryptAsync(
            [LogMessage1, LogMessage2, LogMessage3],
            options,
            decryptionOptions
        );

        // Assert
        decrypted.ShouldContain(LogMessage1);
        decrypted.ShouldContain(LogMessage2);
        decrypted.ShouldContain(LogMessage3);
    }
}
