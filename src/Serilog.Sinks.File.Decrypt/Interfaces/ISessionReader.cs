using System.Security.Cryptography;
using Serilog.Sinks.File.Decrypt.Models;

namespace Serilog.Sinks.File.Decrypt.Interfaces;

/// <summary>
/// The ISessionReader interface defines the contract for reading and decrypting the session header from an encrypted log file. Implementations of this interface are responsible for extracting the AES session key and nonce from the encrypted header using the appropriate RSA private key from the provided key map.
/// This allows subsequent log entries to be decrypted using the obtained session key and nonce.
/// </summary>
internal interface ISessionReader
{
    /// <summary>
    /// Reads the session header from the input stream and decrypts it to obtain the AES session key and nonce.
    /// The session header is expected to be encrypted using RSA, and will be decrypted using the provided <see cref="IKeyProvider"/>
    /// </summary>
    /// <param name="input">The input to decrypt.</param>
    /// <param name="keyProvider">The key provider used to decrypt the AES session key and nonce.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="DecryptionContext"/> containing the AES key, nonce for decrypting subsequent messages.</returns>
    /// <exception cref="EndOfStreamException">The end of the stream is reached before the full session info is read.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no valid RSA key to decrypt the header.</exception>
    /// <exception cref="CryptographicException">Thrown when decryption of the header failed.</exception>
    /// <exception cref="InvalidDataException">Thrown when then header is not valid.</exception>
    internal Task<DecryptionContext> ReadSessionAsync(
        Stream input,
        IKeyProvider keyProvider,
        CancellationToken cancellationToken
    );
}
