using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt.Interfaces;

/// <summary>
/// Interface for writing the framed session data to the output stream according to the specified format.
/// This interface and the implementation should not be changed as it defines the core framing format for the encrypted log file
/// including the magic bytes, version, session length, header, and encrypted messages.
/// </summary>
public interface IFrameWriter
{
    /// <summary>
    /// Responsible for writing the session header to the output stream according to the specified format:
    /// 1. Magic bytes (8 bytes)
    /// 2. Version (1 byte)
    /// 3. Session length (4 bytes, big-endian)
    /// 4. Header (variable length, RSA encrypted)
    /// </summary>
    /// <param name="output">The output stream to which the session header will be written.</param>
    /// <param name="version">The version of the encrypted log format being used.</param>
    /// <param name="keyId"></param>
    /// <param name="header">The RSA encrypted header bytes containing the session metadata.</param>
    /// <param name="sessionLength">The total length of the session data, including the header and encrypted messages, used for framing purposes.</param>
    void WriteHeader(
        Stream output,
        byte version,
        ReadOnlyMemory<byte> keyId,
        byte[] header,
        int sessionLength
    );
}
