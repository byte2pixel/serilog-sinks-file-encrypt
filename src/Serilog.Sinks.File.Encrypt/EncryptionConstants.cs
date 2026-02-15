namespace Serilog.Sinks.File.Encrypt;

internal static class EncryptionConstants
{
    public static readonly byte[] Marker = [0xFF, 0xFF, 0xFF, 0xFF];

    /// <summary>
    /// The fixed magic bytes that identify the file format.
    /// 0x00: Reserved byte (must be 0)
    /// 0x42, 0x32, 0x50: ASCII "B2P" (stands for "Byte2Pixel")
    /// 0xFF, 0xDA, 0x7E: Random bytes for additional uniqueness
    /// 0x00: Reserved byte (must be 0)
    /// </summary>
    public static readonly byte[] MagicBytes = [0x00, 0x42, 0x32, 0x50, 0xFF, 0xDA, 0x7E, 0x00];
    public const int NonceLength = 12; // 96 bits, NIST recommended for AES-GCM
    public const int SessionKeyLength = 32; // 256 bits for AES-256
    public const int TagLength = 16; // 128 bits for AES-GCM authentication tag
}
