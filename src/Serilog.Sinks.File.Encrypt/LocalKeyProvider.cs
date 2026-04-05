using System.Security.Cryptography;
using Serilog.Sinks.File.Encrypt.Interfaces;

namespace Serilog.Sinks.File.Encrypt;

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
    {
        ArgumentNullException.ThrowIfNull(keyMap);
        if (keyMap.Count == 0)
        {
            throw new ArgumentException("No RSA private key found.");
        }

        foreach ((string key, string rsa) in keyMap)
        {
            try
            {
                var rsaKey = RSA.Create();
                rsaKey.FromString(rsa);
                _rsaKeyCache.Add(key, rsaKey);
            }
            catch (Exception ex)
                when (ex is CryptographicException or ArgumentNullException or ArgumentException
                    || ex.GetType().Name.Contains("CryptographicException")
                )
            {
                throw new CryptographicException(
                    $"Invalid RSA key for key ID '{key}': {ex.Message}",
                    ex
                );
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalKeyProvider"/> class with a single RSA key.
    /// </summary>
    /// <param name="keyId"></param>
    /// <param name="privateKey"></param>
    /// <exception cref="CryptographicException"></exception>
    public LocalKeyProvider(string keyId, string privateKey)
    {
        try
        {
            var rsa = RSA.Create();
            rsa.FromString(privateKey);
            _rsaKeyCache.Add(keyId, rsa);
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
