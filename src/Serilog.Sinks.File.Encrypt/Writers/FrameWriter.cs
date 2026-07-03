using Serilog.Sinks.File.Encrypt.Interfaces;

namespace Serilog.Sinks.File.Encrypt;

/// <inheritdoc />
internal class FrameWriter : IFrameWriter
{
    /// <inheritdoc />
    public int WriteHeader(
        Span<byte> destination,
        byte version,
        ReadOnlySpan<byte> keyId,
        ReadOnlySpan<byte> header
    )
    {
        int offset = 0;

        // Write the magic bytes
        CryptographicUtils.MagicBytes.CopyTo(destination);
        offset += CryptographicUtils.MagicBytes.Length;

        // Write the encrypted log format version
        destination[offset++] = version;

        // Write the key ID bytes
        keyId.CopyTo(destination[offset..]);
        offset += keyId.Length;

        // Write the header bytes
        header.CopyTo(destination[offset..]);
        offset += header.Length;

        return offset;
    }
}
