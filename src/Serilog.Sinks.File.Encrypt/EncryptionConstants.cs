namespace Serilog.Sinks.File.Encrypt;

internal static class EncryptionConstants
{
    /// <summary>
    /// The fixed magic bytes that identify the file format.
    /// 0xFF: Reserved byte (must be 0xFF) to easily detect when parsing messages, that a new session started.
    /// 0x42, 0x32, 0x50: ASCII "B2P" (stands for "Byte2Pixel")
    /// 0xFF, 0xDA, 0x7E: Random bytes for additional uniqueness
    /// 0x00: Reserved byte (must be 0)
    /// </summary>
    public static readonly byte[] MagicBytes = [0xFF, 0x42, 0x32, 0x50, 0xFF, 0xDA, 0x7E, 0x00];

    /// <summary>
    /// Precomputed integer value of the first 4 magic bytes for efficient detection.
    /// </summary>
    public static int MagicByteDetection => -12438960;

    /// <summary>
    /// 96 bits, NIST recommended for AES-GCM
    /// </summary>
    public const int NonceLength = 12;

    /// <summary>
    /// 256 bits for AES-256
    /// </summary>
    public const int SessionKeyLength = 32;

    /// <summary>
    /// 128 bits for AES-GCM authentication tag
    /// </summary>
    public const int TagLength = 16;

    /// <summary>
    /// Minimum RSA key size in bits for secure encryption
    /// </summary>
    public const int MinimumRsaKeySize = 2048;
}
