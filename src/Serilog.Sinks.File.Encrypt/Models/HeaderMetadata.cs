using System.Security.Cryptography;

namespace Serilog.Sinks.File.Encrypt.Models;

/// <summary>
/// Describes the structure and requirements of a specific header format version.
/// This metadata is used for validation, error messages, and ensuring compatibility across versions.
/// </summary>
internal sealed class HeaderMetadata
{
    /// <summary>
    /// Gets the version number of this header format.
    /// </summary>
    public int Version { get; init; }

    /// <summary>
    /// Gets the fixed overhead in bytes (excluding variable-length fields like KeyId).
    /// This includes length prefixes, fixed-size fields (AES key, nonce, timestamp), etc.
    /// </summary>
    public int FixedOverheadBytes { get; init; }

    /// <summary>
    /// Gets the RSA encryption padding mode used for this header format.
    /// </summary>
    public RSAEncryptionPadding Padding { get; init; } = RSAEncryptionPadding.OaepSHA256;

    /// <summary>
    /// Gets the recommended buffer size to reserve for future format expansion.
    /// </summary>
    public int ReservedBufferBytes { get; init; }

    /// <summary>
    /// Gets a human-readable description of the fixed fields in this header format.
    /// </summary>
    public string FieldDescription { get; private init; } = string.Empty;

    /// <summary>
    /// Calculates the maximum allowed size for variable-length fields (like KeyId).
    /// </summary>
    /// <param name="rsaKeySize">The RSA key size in bits.</param>
    /// <returns>The maximum size in bytes available for variable-length fields.</returns>
    public int GetMaxVariableFieldSize(int rsaKeySize)
    {
        int maxPlaintext = RsaEncryptionHelper.GetMaxPlaintextSize(rsaKeySize, Padding);
        int availableBytes = maxPlaintext - FixedOverheadBytes - ReservedBufferBytes;
        return Math.Max(0, availableBytes);
    }

    /// <summary>
    /// Creates metadata for Version 1 header format.
    /// </summary>
    public static HeaderMetadata CreateV1()
    {
        return new HeaderMetadata
        {
            Version = 1,
            // 1 byte KeyId length + 1 byte AES length + 32 bytes AES key + 1 byte nonce length + 12 bytes nonce + 8 bytes timestamp
            FixedOverheadBytes = 1 + 1 + 32 + 1 + 12 + 8,
            Padding = RSAEncryptionPadding.OaepSHA256,
            ReservedBufferBytes = 16,
            FieldDescription =
                "KeyIdLen(1) + KeyId(var) + AESLen(1) + AESKey(32) + NonceLen(1) + Nonce(12) + Timestamp(8)",
        };
    }
}
