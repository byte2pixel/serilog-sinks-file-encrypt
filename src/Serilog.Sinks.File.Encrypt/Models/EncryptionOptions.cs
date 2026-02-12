using System.Security.Cryptography;

namespace Serilog.Sinks.File.Encrypt.Models;

/// <summary>
/// Encryption options for Serilog.Sinks.File.Encrypt configuration.
/// This information will be stored in the header of the encrypted log file to allow decryption tools
/// to identify the encryption method, key, and version used for encryption.
///
/// The <see cref="Version"/> property allows for future-proofing the format of the header, enabling backward compatibility.
/// The <see cref="KeyId"/> can be used to identify which key was used for encryption (Key rotation)
/// The <see cref="PublicKey"/> is used to RSA encrypt the AES-GCM session information.
/// </summary>
/// <param name="PublicKey">The RSA public key used to encrypt the AES-GCM session information.</param>
/// <param name="KeyId">The key id to include in the header for key rotation. Allows decryption tools to identify which key was used for encryption.</param>
/// <param name="Version">The version of the header format. This allows for future-proofing the format of the header, enabling backward compatibility.</param>
public record EncryptionOptions(RSA PublicKey, string? KeyId, int Version = 1);
