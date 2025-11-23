namespace Serilog.Sinks.File.Encrypt.Models;

/// <summary>
/// Represents the current decryption context with active encryption keys
/// </summary>
internal record DecryptionContext(byte[] Key, byte[] Iv)
{
    public static DecryptionContext Empty => new([], []);
    public bool HasKeys => Key.Length > 0 && Iv.Length > 0;
}
