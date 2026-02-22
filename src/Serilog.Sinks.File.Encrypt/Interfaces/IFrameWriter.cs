namespace Serilog.Sinks.File.Encrypt.Interfaces;

/// <summary>
/// Interface for writing the framed session data to the output stream according to the specified format.
/// This interface and the implementation should not be changed as it defines the core framing format for the encrypted log file
/// including the magic bytes, version, key ID, and header.
/// </summary>
internal interface IFrameWriter
{
    /// <summary>
    /// Responsible for writing the session header to the output stream according to the specified format:
    /// 1. Magic bytes (8 bytes)
    /// 2. Version (1 byte)
    /// 3. Key ID (32 bytes, UTF-8, null-padded)
    /// 4. Header (variable length, RSA encrypted)
    /// Messages following the header are self-framing with length prefixes.
    /// </summary>
    /// <param name="output">The output stream to which the session header will be written.</param>
    /// <param name="version">The version of the encrypted log format being used.</param>
    /// <param name="keyId">The key identifier for RSA key lookup during decryption.</param>
    /// <param name="header">The RSA encrypted header bytes containing the session metadata.</param>
    internal void WriteHeader(
        Stream output,
        byte version,
        ReadOnlySpan<byte> keyId,
        ReadOnlySpan<byte> header
    );
}
