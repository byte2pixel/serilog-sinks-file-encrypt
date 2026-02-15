using System.Security.Cryptography;

namespace Serilog.Sinks.File.Encrypt.Interfaces;

/// <summary>
/// The <see cref="IHeaderDecryptor"/> interface defines the contract for decoding the session header information.
/// </summary>
public interface IHeaderDecryptor
{
    /// <summary>
    /// Decrypts the session header information, which includes the RSA-encrypted session key and nonce.
    /// </summary>
    /// <param name="rsa">The RSA instance containing the private key corresponding to the public key used for encryption.
    /// This is used to decrypt the AES session key and nonce from the header data.</param>
    /// <param name="headerData">The encrypted header data read from the log file.</param>
    /// <returns>A tuple containing the decrypted AES session key, nonce, and timestamp.</returns>
    (byte[] AesKey, byte[] Nonce) Decrypt(RSA rsa, ReadOnlySpan<byte> headerData);
}
