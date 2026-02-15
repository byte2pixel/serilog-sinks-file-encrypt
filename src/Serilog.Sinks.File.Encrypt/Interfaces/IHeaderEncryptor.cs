using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt.Interfaces;

/// <summary>
/// The <see cref="IHeaderEncryptor"/> interface defines the contract for encoding the session header information,
/// which includes the RSA-encrypted session key and nonce.
/// This header is essential for securely transmitting the session key and nonce to the decryption tool,
/// allowing it to decrypt the log messages correctly.
/// </summary>
public interface IHeaderEncryptor
{
    /// <summary>
    /// Encodes the session header information, which includes the RSA-encrypted session key and nonce.
    /// </summary>
    /// <param name="aesKey">The AES session key.</param>
    /// <param name="nonce">The AES-GCM nonce.</param>
    /// <returns></returns>
    ReadOnlySpan<byte> Encrypt(ReadOnlySpan<byte> aesKey, ReadOnlySpan<byte> nonce);
}
