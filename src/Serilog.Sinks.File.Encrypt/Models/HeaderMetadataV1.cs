using System.Security.Cryptography;

namespace Serilog.Sinks.File.Encrypt.Models;

/// <summary>
/// Represents the metadata for Version 1 of the header format, which includes specific lengths for various
/// components.
/// </summary>
public static class HeaderMetadataV1
{
    /// <summary>
    /// Version 1 key ID length in bytes. This is the length of the key identifier string that will be stored in the header.
    /// </summary>
    public const int KeyIdLength = 32;

    /// <summary>
    /// Version 1 AES key length in bytes. This is the length of the AES key that will be encrypted and stored in the header.
    /// </summary>
    public const int AesKeyLength = 32;

    /// <summary>
    /// Version 1 nonce length.
    /// </summary>
    public const int NonceLength = 12;

    /// <summary>
    /// The padding scheme used for RSA in the version 1 header.
    /// </summary>
    public static RSAEncryptionPadding Padding => RSAEncryptionPadding.OaepSHA256;

    /// <summary>
    /// The total length of the RSA payload for the version 1 header.
    /// This is the combined length of the AES key and nonce.
    /// </summary>
    public static int RsaPayloadLength => AesKeyLength + NonceLength;
}
