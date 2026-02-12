using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt.Writers;

/// <summary>
/// Utility class responsible for writing the framed session data to the output stream according to the specified format.
/// This class will should not be changed as it defines the core framing format for the encrypted log file
/// including the magic bytes, version, session length, header, and encrypted messages.
/// </summary>
public static class FrameWriter
{
    // csharpier-ignore
    /// <summary>
    /// The fixed magic bytes that identify the file format.
    /// 0x00: Reserved byte (must be 0)
    /// 0x42, 0x32, 0x50: ASCII "B2P" (stands for "Byte2Pixel")
    /// 0xFF, 0xDA, 0x7E: Random bytes for additional uniqueness
    /// 0x00: Reserved byte (must be 0)
    /// </summary>
    private static readonly byte[] _magicBytes =
    [
        0x00, 0x42, 0x32, 0x50, 0xFF, 0xDA, 0x7E, 0x00
    ];

    /// <summary>
    /// Writes the session header to the output stream according to the specified format:
    /// 1. Magic bytes (8 bytes)
    /// 2. Version (1 byte)
    /// 3. Session length (4 bytes, big-endian)
    /// 4. Header (variable length, RSA encrypted)
    /// </summary>
    /// <param name="output"></param>
    /// <param name="version"></param>
    /// <param name="header"></param>
    /// <param name="sessionLength"></param>
    public static void WriteHeader(Stream output, byte version, byte[] header, int sessionLength)
    {
        // Write the magic bytes
        output.Write(_magicBytes, 0, _magicBytes.Length);
        // Write the encrypted log format version
        output.WriteByte(version);
        // Write the session length as a 4-byte integer (big-endian)
        byte[] sessionLengthBytes = BitConverter.GetBytes(sessionLength);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(sessionLengthBytes);
        }

        output.Write(sessionLengthBytes, 0, sessionLengthBytes.Length);

        // Write the header bytes
        output.Write(header, 0, header.Length);
    }

    public static void WriteMessage(Stream output, EncryptedMessage encryptedMessage)
    {
        // Write the encrypted message length as a 4-byte integer (big-endian)
        byte[] messageLengthBytes = BitConverter.GetBytes(encryptedMessage.TotalLength);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(messageLengthBytes);
        }

        output.Write(messageLengthBytes, 0, messageLengthBytes.Length);

        // Write the encrypted message bytes
        output.Write(encryptedMessage.Ciphertext, 0, encryptedMessage.Ciphertext.Length);
        output.Write(encryptedMessage.Tag, 0, encryptedMessage.Tag.Length);
    }
}
