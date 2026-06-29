using System.Security.Cryptography;
using Serilog.Sinks.File.Decrypt.Interfaces;
using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Decrypt;

/// <inheritdoc />
internal class HeaderReader : IHeaderReader
{
    /// <summary>
    /// Decrypts the session header information, which includes the RSA-encrypted session key and nonce.
    /// </summary>
    /// <param name="keyProvider">Provides a method for decrypting the AES-GCM session key and nonce.</param>
    /// <param name="keyId">The key id that was used to encrypt the AES-GCM session key and nonce</param>
    /// <param name="headerData">The encrypted header data read from the log file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing the decrypted AES session key and nonce.</returns>
    /// <exception cref="CryptographicException">Thrown when RSA decryption of the header fails.</exception>
    /// <exception cref="InvalidDataException">Thrown when the decrypted payload is too short to contain the expected AES key and nonce.</exception>
    public async Task<(byte[] AesKey, byte[] Nonce)> Decrypt(
        IKeyProvider keyProvider,
        string keyId,
        ReadOnlyMemory<byte> headerData,
        CancellationToken cancellationToken = default
    )
    {
        int offset = 0;

        // Decrypt the RSA payload
        byte[] decryptedPayload;
        try
        {
            decryptedPayload = await keyProvider.DecryptAsync(keyId, headerData, cancellationToken);
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
        // Read AES key
        if (decryptedPayload.Length < HeaderMetadata.AesKeyLength)
        {
            throw new InvalidDataException("Decrypted payload is too short to read AES key");
        }

        byte[] aesKey = decryptedPayload[offset..(HeaderMetadata.AesKeyLength)];
        offset += HeaderMetadata.AesKeyLength;

        if (decryptedPayload.Length < offset + HeaderMetadata.NonceLength)
        {
            throw new InvalidDataException("Decrypted payload is too short to read the nonce.");
        }

        byte[] nonce = decryptedPayload[offset..(offset + HeaderMetadata.NonceLength)];

        return (aesKey, nonce);
    }
}
