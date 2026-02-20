using System.Security.Cryptography;
using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt.Interfaces;

/// <summary>
/// The ISessionReader interface defines the contract for reading and decrypting the session header from an encrypted log file. Implementations of this interface are responsible for extracting the AES session key and nonce from the encrypted header using the appropriate RSA private key from the provided key map.
/// This allows subsequent log entries to be decrypted using the obtained session key and nonce.
/// </summary>
internal interface ISessionReader
{
    /// <summary>
    /// Reads the session header from the input stream, decrypts it using the appropriate RSA key from the key map
    /// </summary>
    /// <param name="input">The input to decrypt.</param>
    /// <param name="keyMap">The key map to look up the proper RSA key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A DecryptionContext containing the AES key, nonce for decrypting subsequent messages.</returns>
    internal Task<DecryptionContext> ReadSessionAsync(
        Stream input,
        Dictionary<string, RSA> keyMap,
        CancellationToken cancellationToken
    );
}
