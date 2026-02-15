using System.Security.Cryptography;
using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt.Interfaces;

/// <summary>
/// The <see cref="ISessionReader"/> interface defines the contract for reading and decrypting session information from
/// encrypted log entries, including the AES session key, nonce, and timestamp, using RSA decryption for the header.
/// </summary>
public interface ISessionReader
{
    /// <summary>
    /// Reads the session information from the given header, using the provided RSA instance to decrypt the session key and nonce.
    /// Messages following the header are read separately using IMessageDecryptor.
    /// </summary>
    /// <returns>A DecryptionContext containing the AES key, nonce, and timestamp for decrypting subsequent messages.</returns>
    Task<DecryptionContext> ReadSessionAsync(
        Stream input,
        Dictionary<string, RSA> keyMap,
        CancellationToken cancellationToken
    );
}
