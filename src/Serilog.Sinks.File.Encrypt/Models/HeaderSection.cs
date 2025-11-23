namespace Serilog.Sinks.File.Encrypt.Models;

/// <summary>
/// Represents a parsed header section from the encrypted file
/// </summary>
internal record HeaderSection(byte[] EncryptedKey, byte[] EncryptedIv);
