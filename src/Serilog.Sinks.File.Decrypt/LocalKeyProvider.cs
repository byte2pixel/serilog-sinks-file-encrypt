using System.Security.Cryptography;
using Serilog.Sinks.File.Decrypt.Interfaces;
using Serilog.Sinks.File.Encrypt;

namespace Serilog.Sinks.File.Decrypt;

/// <summary>
/// The <see cref="LocalKeyProvider"/> class is an implementation of the <see cref="IKeyProvider"/> interface that
/// provides RSA decryption capabilities using a local in-memory cache of RSA keys.
/// This class allows for the decryption of AES-GCM session keys and nonces that are encrypted with RSA,
/// which are used to encrypt log entries.
/// </summary>
public sealed class LocalKeyProvider : IKeyProvider, IDisposable
{
    private readonly Dictionary<string, RSA> _rsaKeyCache = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalKeyProvider"/> class with a dictionary of key IDs
    /// and their corresponding RSA private keys in string format.
    /// </summary>
    /// <param name="keyMap">A dictionary mapping key IDs to their corresponding RSA private keys in string format.
    /// The keys in the dictionary represent the key IDs that will be used to look up the RSA keys for decryption,
    /// and the values are the RSA private keys in a format that can be parsed by the RSA class (e.g. XML, PEM format).
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown if the provided keyMap is null.</exception>
    /// <exception cref="ArgumentException">Thrown if the provided keyMap is empty</exception>
    /// <exception cref="CryptographicException">Thrown if any of the RSA keys in the keyMap are invalid or cannot be parsed.</exception>
    public LocalKeyProvider(Dictionary<string, string> keyMap)
        : this(keyMap, passphrase: null) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalKeyProvider"/> class with a dictionary of key IDs
    /// and their corresponding RSA private keys, where keys may be passphrase-encrypted PKCS#8 PEM
    /// (<c>ENCRYPTED PRIVATE KEY</c> blocks). The same passphrase is applied to every encrypted key
    /// in the map; unencrypted keys ignore it.
    /// </summary>
    /// <param name="keyMap">A dictionary mapping key IDs to their corresponding RSA private keys in string format.</param>
    /// <param name="passphrase">The passphrase for encrypted keys, or null when no key is encrypted.</param>
    /// <exception cref="ArgumentNullException">Thrown if the provided keyMap is null.</exception>
    /// <exception cref="ArgumentException">Thrown if the provided keyMap is empty</exception>
    /// <exception cref="CryptographicException">Thrown if any RSA key is invalid, encrypted without a passphrase, or the passphrase is wrong.</exception>
    public LocalKeyProvider(Dictionary<string, string> keyMap, string? passphrase)
    {
        ArgumentNullException.ThrowIfNull(keyMap);
        if (keyMap.Count == 0)
        {
            throw new ArgumentException("No RSA private key found.");
        }

        foreach ((string key, string rsa) in keyMap)
        {
            _rsaKeyCache.Add(key, ImportKey(key, rsa, passphrase));
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalKeyProvider"/> class with a single RSA key.
    /// </summary>
    /// <param name="keyId">The key id associated with the private key.</param>
    /// <param name="privateKey">The RSA private key in XML or PEM format.</param>
    /// <exception cref="CryptographicException">Thrown if the RSA key is invalid or cannot be parsed.</exception>
    public LocalKeyProvider(string keyId, string privateKey)
        : this(keyId, privateKey, passphrase: null) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalKeyProvider"/> class with a single RSA key
    /// that may be a passphrase-encrypted PKCS#8 PEM (<c>ENCRYPTED PRIVATE KEY</c> block).
    /// An unencrypted key ignores the passphrase.
    /// </summary>
    /// <param name="keyId">The key id associated with the private key.</param>
    /// <param name="privateKey">The RSA private key in XML or PEM format, optionally encrypted.</param>
    /// <param name="passphrase">The passphrase for an encrypted key, or null when the key is unencrypted.</param>
    /// <exception cref="CryptographicException">Thrown if the RSA key is invalid, encrypted without a passphrase, or the passphrase is wrong.</exception>
    public LocalKeyProvider(string keyId, string privateKey, string? passphrase)
    {
        _rsaKeyCache.Add(keyId, ImportKey(keyId, privateKey, passphrase));
    }

    private static RSA ImportKey(string keyId, string privateKey, string? passphrase)
    {
        try
        {
            var rsa = RSA.Create();
            if (passphrase is null)
            {
                rsa.FromString(privateKey);
            }
            else
            {
                rsa.FromString(privateKey, passphrase);
            }

            return rsa;
        }
        catch (Exception ex)
            when (ex is CryptographicException or ArgumentNullException or ArgumentException
                || ex.GetType().Name.Contains("CryptographicException")
            )
        {
            throw new CryptographicException(
                $"Invalid RSA key for key ID '{keyId}': {ex.Message}",
                ex
            );
        }
    }

    /// <summary>
    /// Decrypts the given cipher text using the decryption key associated with the provided key ID.
    /// The cipher text is expected to contain the AES-GCM encrypted session key and nonce, which are necessary for
    /// decrypting the log entries. Implementations of this method should handle the logic for decrypting
    /// the cipher text and returning the decrypted session key and nonce as a byte array.
    /// </summary>
    /// <param name="keyId">The key id that was used to encrypt the AES-GCM session key and nonce</param>
    /// <param name="cipherText">The AES-GCM encrypted session key and nonce that needs to be decrypted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The decrypted AES-GCM session key and nonce as a byte array.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown if there is no RSA private key found for the provided key ID.</exception>
    /// <exception cref="CryptographicException">Thrown when RSA decryption of the header fails.</exception>
    /// <remarks>
    /// This class implementation of DecryptAsync is not thread safe.
    /// </remarks>
    public Task<byte[]> DecryptAsync(
        string keyId,
        ReadOnlyMemory<byte> cipherText,
        CancellationToken cancellationToken = default
    )
    {
        _rsaKeyCache.TryGetValue(keyId, out RSA? rsa);
        if (rsa == null)
        {
            throw new InvalidOperationException($"No RSA private key found for KeyId: '{keyId}'.");
        }
        // Decrypt the RSA payload
        byte[] decryptedPayload;
        try
        {
            decryptedPayload = rsa.Decrypt(cipherText.Span, RSAEncryptionPadding.OaepSHA256);
        }
        // this catch is needed because of Interop+Crypto+OpenSslCryptographicException on GitHub Actions Ubuntu runners.
        // The OpenSSL implementation throws a different exception type that derives from CryptographicException, so we catch both.
        catch (Exception ex)
            when (ex is CryptographicException
                || ex.GetType().Name.Contains("CryptographicException")
            )
        {
            throw new CryptographicException("RSA decryption of header failed.", ex);
        }
        return Task.FromResult(decryptedPayload);
    }

    /// <summary>
    /// Returns the key size in bits for the RSA key associated with the provided key ID.
    /// </summary>
    /// <param name="keyId">The key id that was used to encrypt the AES-GCM session key and nonce</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// An integer representing the key size in bits for the RSA key associated with the provided key ID.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown if there is no RSA private key found for the provided key ID.</exception>
    /// <remarks>
    /// This implementation does not need to cache the key sizes since it is all in memory, but any implementations
    /// that require an external call to retrieve the key size should consider caching the key sizes to avoid unnecessary calls.
    /// </remarks>
    public Task<int> GetKeySizeAsync(string keyId, CancellationToken cancellationToken = default)
    {
        _rsaKeyCache.TryGetValue(keyId, out RSA? rsa);
        return rsa == null
            ? throw new InvalidOperationException("No RSA private key found.")
            : Task.FromResult(rsa.KeySize);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeRsaKeys();
    }

    private void DisposeRsaKeys()
    {
        foreach (RSA value in _rsaKeyCache.Values)
        {
            value.Dispose();
        }
    }
}
