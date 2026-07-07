namespace Serilog.Sinks.File.Encrypt.Cli;

/// <summary>
/// Writes generated key files to disk, restricting private key files to owner-only access
/// (mode 600 on Unix, an owner-only DACL on Windows). Public keys are written with default
/// permissions. Failure to restrict permissions is reported as a warning, never a failure —
/// the key material itself is still written.
/// </summary>
public interface IKeyFileWriter
{
    /// <summary>
    /// Writes the private key and restricts the file to the current user.
    /// </summary>
    /// <param name="path">Destination file path.</param>
    /// <param name="contents">The private key text.</param>
    void WritePrivateKey(string path, string contents);

    /// <summary>
    /// Writes the public key with default permissions.
    /// </summary>
    /// <param name="path">Destination file path.</param>
    /// <param name="contents">The public key text.</param>
    void WritePublicKey(string path, string contents);
}
