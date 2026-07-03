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

    /// <summary>
    /// The original on-disk format: AES-GCM frames with no associated data and no seal record.
    /// Read-only since v6.0.0; the writer no longer emits it.
    /// </summary>
    public const byte FormatVersionV1 = 1;

    /// <summary>
    /// The v2 on-disk format: per-frame associated data (header hash + frame sequence + frame type)
    /// and an authenticated end-of-log seal record written on clean close.
    /// </summary>
    public const byte FormatVersionV2 = 2;

    /// <summary>
    /// The format version the writer emits.
    /// </summary>
    public const byte CurrentFormatVersion = FormatVersionV2;

    /// <summary>
    /// Length of the SHA-256 hash of the session header that is bound into each frame's
    /// associated data (v2).
    /// </summary>
    public const int HeaderHashLength = 32;

    /// <summary>
    /// Length of the big-endian frame sequence number in the associated data (v2).
    /// </summary>
    public const int FrameSequenceLength = sizeof(ulong);

    /// <summary>
    /// Length of the frame-type discriminator byte in the associated data (v2).
    /// </summary>
    public const int FrameTypeLength = 1;

    /// <summary>
    /// Total length of the associated data bound into every v2 AES-GCM record:
    /// headerHash(32) + frameSequence(8, big-endian) + frameType(1).
    /// </summary>
    public const int AadLength = HeaderHashLength + FrameSequenceLength + FrameTypeLength;

    /// <summary>
    /// Frame-type discriminator for a data frame (v2 associated data).
    /// </summary>
    public const byte FrameTypeData = 0x00;

    /// <summary>
    /// Frame-type discriminator for the end-of-log seal record (v2 associated data).
    /// In the seal's associated data the frame-sequence field is all zero (reserved);
    /// the final frame count travels in the seal's encrypted payload instead.
    /// </summary>
    public const byte FrameTypeSeal = 0x01;

    /// <summary>
    /// On-disk marker that introduces the v2 end-of-log seal record. Written where a data frame's
    /// length prefix would otherwise be; as a negative big-endian int32 it can never collide with
    /// a valid length prefix, and it differs from <see cref="MagicByteDetection"/>.
    /// </summary>
    public static readonly byte[] SealMarkerBytes = [0xFF, 0x42, 0x32, 0x53];

    /// <summary>
    /// Precomputed big-endian int32 value of <see cref="SealMarkerBytes"/> for efficient detection.
    /// </summary>
    public static int SealMarkerDetection => -12438957;

    /// <summary>
    /// Length of the seal record's plaintext: the final frame count as a big-endian ulong.
    /// </summary>
    public const int SealPlaintextLength = sizeof(ulong);

    /// <summary>
    /// Bytes of the seal record that follow the 4-byte marker: ciphertext(8) + tag(16).
    /// A tail shorter than this after the marker is a partially written seal (unclean close).
    /// </summary>
    public const int SealRecordRemainderLength = SealPlaintextLength + TagLength;
}
