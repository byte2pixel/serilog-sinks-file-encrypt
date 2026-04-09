namespace Serilog.Sinks.File.Encrypt;

internal static class EncryptionConstants
{
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
