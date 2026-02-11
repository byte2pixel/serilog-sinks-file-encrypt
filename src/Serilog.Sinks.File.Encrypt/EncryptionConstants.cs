namespace Serilog.Sinks.File.Encrypt;

internal static class EncryptionConstants
{
    public static readonly byte[] Marker = [0xFF, 0xFF, 0xFF, 0xFF];
    public static readonly byte[] Version = [0x01, 0x00, 0x00, 0x00];
    public const int NonceLength = 12; // 96 bits, NIST recommended for AES-GCM
    public const int SessionKeyLength = 32; // 256 bits for AES-256
    public const int TagLength = 16; // 128 bits for AES-GCM authentication tag
    public const int SizeOfInt = sizeof(int);
    public const int HeaderUnencryptedSize = SizeOfInt + NonceLength + SessionKeyLength;
    public const int HeaderNonceOffset = SizeOfInt;
    public const int HeaderSessionKeyOffset = SizeOfInt + NonceLength;
    public const int MinRsaKeySize = 2048; // Minimum RSA key size in bits for secure encryption
}
