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
    /// Reads the session information from the given header and payload, using the provided RSA instance to decrypt the session key and nonce.
    /// </summary>
    /// <param name="rsa">The RSA used to encrypt the header</param>
    /// <param name="header">The header to be decrypted</param>
    /// <param name="payload">The payload</param>
    /// <returns></returns>
    public DecryptionSessionChunk ReadSession(
        RSA rsa,
        ReadOnlyMemory<byte> header,
        ReadOnlyMemory<byte> payload
    );
}
