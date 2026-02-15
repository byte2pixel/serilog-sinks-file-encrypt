using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt.Interfaces;

/// <summary>
/// The <see cref="IMessageDecryptor"/> interface defines the contract for decrypting encrypted log messages using AES-GCM.
/// </summary>
public interface IMessageDecryptor
{
    /// <summary>
    /// Decrypts the given encrypted log message using the provided AES session key and nonce.
    /// </summary>
    /// <param name="sessionChunk">
    /// The <see cref="DecryptionSessionChunk"/> containing the AES session key, nonce, and the encrypted log message payload.
    /// </param>
    /// <returns>
    /// A <see cref="ReadOnlyMemory{Byte}"/> containing the decrypted log message data.
    /// </returns>
    ReadOnlyMemory<byte> ReadAndDecrypt(DecryptionSessionChunk sessionChunk);
}
