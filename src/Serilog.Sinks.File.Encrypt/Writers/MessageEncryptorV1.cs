using System.Security.Cryptography;
using Serilog.Sinks.File.Encrypt.Interfaces;
using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt.Writers;

/// <summary>
/// Implements AES-GCM encryption for log messages. This encryptor uses a fixed tag length defined in <see cref="EncryptionConstants.TagLength"/>.
/// </summary>
internal class MessageEncryptorV1 : IMessageEncryptor
{
    /// <inheritdoc />
    public EncryptedMessage Encrypt(byte[] plaintext, byte[] key, byte[] nonce)
    {
        using var aes = new AesGcm(key, EncryptionConstants.TagLength);

        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[EncryptionConstants.TagLength]; // fixed GCM tag size

        aes.Encrypt(nonce, plaintext, ciphertext, tag, associatedData: null);

        return new EncryptedMessage { Ciphertext = ciphertext, Tag = tag };
    }
}
