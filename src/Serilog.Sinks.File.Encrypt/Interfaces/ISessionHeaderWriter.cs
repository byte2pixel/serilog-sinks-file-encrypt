namespace Serilog.Sinks.File.Encrypt.Interfaces;

/// <summary>
/// Interface for writing session headers to an output stream.
/// The session header contains the magic bytes, version, key ID, and RSA-encrypted session information.
/// This is written once per session, followed by multiple messages encrypted with the session's AES key.
/// </summary>
internal interface ISessionHeaderWriter
{
    /// <summary>
    /// Writes the session header to the output stream.
    /// This includes magic bytes, version, key ID, and RSA-encrypted session data (AES key, nonce, timestamp).
    /// </summary>
    /// <param name="output">The stream to write the header to.</param>
    /// <param name="aesKey">The AES key for the session, which will be encrypted with RSA and included in the header.</param>
    /// <param name="nonce">The nonce for AES-GCM, which will also be encrypted and included in the header.</param>
    internal void WriteHeader(Stream output, ReadOnlySpan<byte> aesKey, ReadOnlySpan<byte> nonce);
}
