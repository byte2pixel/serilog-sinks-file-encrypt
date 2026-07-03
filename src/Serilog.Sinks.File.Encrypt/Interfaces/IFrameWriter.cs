namespace Serilog.Sinks.File.Encrypt.Interfaces;

/// <summary>
/// Interface for composing the framed session header according to the on-disk format.
/// The layout (magic bytes, version, key ID, RSA payload) is identical for v1 and v2;
/// only the version byte value differs. In the v2 format the composed header bytes are
/// additionally hashed (SHA-256) and bound into every frame's AES-GCM associated data,
/// which is why the header is composed into a contiguous buffer instead of being written
/// piecewise to the stream.
/// </summary>
internal interface IFrameWriter
{
    /// <summary>
    /// Composes the session header into <paramref name="destination"/> according to the format:
    /// 1. Magic bytes (8 bytes)
    /// 2. Version (1 byte)
    /// 3. Key ID (32 bytes, zero-padded)
    /// 4. Header (RSA encrypted bytes, length determined by RSA key size)
    /// Messages following the header are self-framing with length prefixes.
    /// </summary>
    /// <param name="destination">The buffer to compose the session header into. Must be large enough to hold the full header.</param>
    /// <param name="version">The version of the encrypted log format being used.</param>
    /// <param name="keyId">The key identifier for RSA key lookup during decryption.</param>
    /// <param name="header">The RSA encrypted header bytes containing the session metadata.</param>
    /// <returns>The number of bytes written to <paramref name="destination"/>.</returns>
    internal int WriteHeader(
        Span<byte> destination,
        byte version,
        ReadOnlySpan<byte> keyId,
        ReadOnlySpan<byte> header
    );
}
