using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt.Interfaces;

/// <summary>
/// The <see cref="IMessageEncryptor"/> interface defines the contract for encrypting log messages using AES-GCM encryption.
/// </summary>
public interface IMessageEncryptor
{
    /// <summary>
    /// Gets the total length of encrypted output for a given plaintext length.
    /// </summary>
    /// <param name="plaintextLength">The length of the plaintext.</param>
    /// <returns>The total length of the encrypted message (ciphertext + tag)</returns>
    int GetEncryptedLength(int plaintextLength);

    /// <summary>
    /// Encrypts the plaintext and writes the encrypted message (ciphertext + tag) directly to the output stream.
    /// </summary>
    /// <param name="output">The stream to write the encrypted data to.</param>
    /// <param name="session">The AesGcm Session and data</param>
    /// <param name="buffer"></param>
    void EncryptAndWrite(Stream output, SessionData session, ReadOnlySpan<byte> buffer);
}
