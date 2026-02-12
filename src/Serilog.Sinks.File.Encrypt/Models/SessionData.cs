namespace Serilog.Sinks.File.Encrypt.Models;

/// <summary>
/// Data class representing the session information for encrypting a log file. This includes the plaintext bytes, AES key, nonce, and optional metadata such as key ID and timestamp.
/// This information is used by the ISessionWriter to write the encrypted session data to the output stream,
/// and by decryption tools to read and decrypt the log file.
/// </summary>
public class SessionData
{
    /// <summary>
    /// The raw plaintext bytes for this session (everything since last flush)
    /// </summary>
    public byte[] Plaintext { get; init; } = [];

    /// <summary>
    /// The randomly generated AES session key (e.g., 256-bit)
    /// </summary>
    public byte[] AesKey { get; init; } = [];

    /// <summary>
    /// The randomly generated AES-GCM nonce (96-bit recommended)
    /// </summary>
    public byte[] Nonce { get; init; } = [];

    /// <summary>
    /// Optional: metadata for debugging or future versions
    /// </summary>
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
}
