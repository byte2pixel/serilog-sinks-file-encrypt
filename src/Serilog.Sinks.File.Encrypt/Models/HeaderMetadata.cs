using System.Security.Cryptography;

namespace Serilog.Sinks.File.Encrypt.Models;

/// <summary>
/// Describes the structure and requirements of a specific header format version.
/// Values represent the lengths of various components in bytes,
/// which are used to correctly parse and decrypt the header and payload of encrypted log entries.
/// </summary>
public abstract class HeaderMetadata
{
    /// <summary>
    /// The number of bytes used to represent the version number in the header.
    /// </summary>
    public static int Version => 1;

    /// <summary>
    /// The number of bytes used to represent the length of the session length (header + payload).
    /// </summary>
    public static int SessionLength => 4;

    /// <summary>
    /// Gets the RSA encryption padding mode used for this header format.
    /// </summary>
    public static RSAEncryptionPadding Padding => RSAEncryptionPadding.OaepSHA256;

    /// <summary>
    /// The length of the key identifier in bytes, which is used to look up the corresponding RSA key for decryption.
    /// </summary>
    public abstract int KeyIdLength { get; }

    /// <summary>
    /// Creates metadata for Version 1 header format.
    /// </summary>
    public static HeaderMetadataV1 CreateV1()
    {
        // csharpier-ignore
        return new HeaderMetadataV1();
    }
}

/// <summary>
/// Represents the metadata for Version 1 of the header format, which includes specific lengths for various
/// components.
/// </summary>
public class HeaderMetadataV1 : HeaderMetadata
{
    /// <inheritdoc />
    public override int KeyIdLength => 32;

    /// <summary>
    /// The length of the AES key in bytes. Version 1 uses a 256-bit AES key, which is 32 bytes.
    /// </summary>
    public static int AesKeyLength => 32;

    /// <summary>
    /// The length of the nonce in bytes. A 96-bit (12-byte) nonce is commonly used for AES-GCM encryption
    /// </summary>
    public static int NonceLength => 12;

    /// <summary>
    /// The length of the timestamp in bytes. A 64-bit (8-byte) timestamp is sufficient to represent dates for many years with millisecond precision.
    /// </summary>
    public static int TimestampLength => 8;

    /// <summary>
    /// The total length of the data that will be encrypted with RSA, which includes the AES key, nonce, and timestamp.
    /// </summary>
    public static int RsaPayloadLength => AesKeyLength + NonceLength + TimestampLength;
}
