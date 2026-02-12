using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt.Interfaces;

public interface IMessageEncryptor
{
    /// <summary>
    /// Encrypts the given plaintext using the provided key and nonce, returning an <see cref="EncryptedMessage"/>
    /// </summary>
    /// <param name="plaintext">The plaintext log message to be encrypted.</param>
    /// <param name="key">The AES session key used for encryption.</param>
    /// <param name="nonce">The nonce used for AES-GCM encryption.</param>
    /// <returns></returns>
    EncryptedMessage Encrypt(byte[] plaintext, byte[] key, byte[] nonce);
}
