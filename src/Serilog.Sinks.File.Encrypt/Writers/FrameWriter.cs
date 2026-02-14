using System.Buffers.Binary;
using Serilog.Sinks.File.Encrypt.Interfaces;
using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt.Writers;

/// <inheritdoc />
internal class FrameWriter : IFrameWriter
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

    /// <inheritdoc />
    public void WriteHeader(Stream output, byte version, byte[] header, int sessionLength)
    {
        // Write the magic bytes
        output.Write(_magicBytes, 0, _magicBytes.Length);

        // Write the encrypted log format version
        output.WriteByte(version);

        // Write the session length as a 4-byte integer (big-endian) using stackalloc
        Span<byte> sessionLengthBytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(sessionLengthBytes, sessionLength);
        output.Write(sessionLengthBytes);

        // Write the header bytes
        output.Write(header, 0, header.Length);
    }

    /// <inheritdoc />
    public void WriteMessage(Stream output, EncryptedMessage encryptedMessage)
    {
        // Write the encrypted message length as a 4-byte integer (big-endian) using stackalloc
        Span<byte> messageLengthBytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(messageLengthBytes, encryptedMessage.TotalLength);
        output.Write(messageLengthBytes);

        // Write the encrypted message bytes
        output.Write(encryptedMessage.Ciphertext, 0, encryptedMessage.Ciphertext.Length);
        output.Write(encryptedMessage.Tag, 0, encryptedMessage.Tag.Length);
    }
}
