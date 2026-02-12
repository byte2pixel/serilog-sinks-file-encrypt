using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt.Interfaces;

/// <summary>
/// The <see cref="ISessionWriter"/> interface defines the contract for writing encrypted session data to an output stream.
/// </summary>
public interface ISessionWriter
{
    /// <summary>
    /// Takes the session data and writes the encrypted session information to the output stream.
    /// This includes encrypting the plaintext log messages using AES-GCM with the provided session key and nonce,
    /// and then RSA encrypting the session key and nonce with the provided public key.
    /// The encrypted session data is then written to the output stream in a format that can be read by decryption tools.
    /// </summary>
    /// <param name="output">The underlying stream.</param>
    /// <param name="session">All the info that needs encrypted for this session.</param>
    void WriteSession(Stream output, SessionData session);
}
