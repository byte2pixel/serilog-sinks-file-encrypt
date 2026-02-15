using Serilog.Sinks.File.Encrypt.Interfaces;

namespace Serilog.Sinks.File.Encrypt.Writers;

/// <inheritdoc />
internal class FrameWriter : IFrameWriter
{
    /// <inheritdoc />
    public void WriteHeader(Stream output, byte version, ReadOnlyMemory<byte> keyId, byte[] header)
    {
        // Write the magic bytes
        output.Write(EncryptionConstants.MagicBytes, 0, EncryptionConstants.MagicBytes.Length);

        // Write the encrypted log format version
        output.WriteByte(version);

        // Write the key ID bytes
        output.Write(keyId.Span);

        // Write the header bytes
        output.Write(header, 0, header.Length);
    }
}
