using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt.Interfaces;

/// <summary>
/// Interface for writing session headers to an output stream.
/// The session header contains the magic bytes, version, key ID, and RSA-encrypted session information.
/// This is written once per session, followed by multiple messages encrypted with the session's AES key.
/// </summary>
public interface ISessionHeaderWriter
{
    /// <summary>
    /// Writes the session header to the output stream.
    /// This includes magic bytes, version, key ID, and RSA-encrypted session data (AES key, nonce, timestamp).
    /// </summary>
    /// <param name="output">The stream to write the header to.</param>
    /// <param name="session">The session data containing the AES key, nonce, and other session information.</param>
    void WriteHeader(Stream output, ReadOnlySpan<byte> aesKey, ReadOnlySpan<byte> nonce);
}
