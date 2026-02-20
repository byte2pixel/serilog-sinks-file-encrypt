using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt;

/// <summary>
/// Serilog.Sinks.File lifecycle hooks for transparent log file encryption.
/// </summary>
/// <remarks>
/// <para>
/// This class integrates with Serilog.Sinks.File to automatically encrypt log files as they are written.
/// It intercepts file stream creation and wraps it with <see cref="LogWriter"/> for transparent encryption.
/// </para>
/// <para>
/// <b>Thread Safety:</b> This class is thread-safe. Serilog.Sinks.File handles synchronization internally,
/// so concurrent log writes are serialized before reaching the encryption layer.
/// </para>
/// <para>
/// <b>Key Management:</b> The RSA public key is stored in memory for the lifetime of the logger.
/// Private keys should never be passed to this class - only the public key is needed for encryption.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Generate or load RSA keys
/// var (publicKey, privateKey) = EncryptionUtils.GenerateRsaKeyPair();
///
/// // Configure Serilog with encryption
/// Log.Logger = new LoggerConfiguration()
///     .WriteTo.File(
///         path: "logs/app.log",
///         hooks: new EncryptHooks(publicKey),
///         rollingInterval: RollingInterval.Day
///     )
///     .CreateLogger();
///
/// // Logs are now automatically encrypted
/// Log.Information("Sensitive data: {UserId}", userId);
/// </code>
/// </example>
public class EncryptHooks : FileLifecycleHooks
{
    private static readonly ConcurrentDictionary<string, RSA> _rsaCache = new();
    private readonly EncryptionOptions _encryptionOptions;

    /// <summary>
    /// Creates a new instance of <see cref="EncryptHooks"/> with the provided RSA public key.
    /// </summary>
    /// <param name="publicKey">The RSA public key in XML or PEM format. Use <see cref="CryptographicUtils.GenerateRsaKeyPair"/> to generate keys.</param>
    /// <param name="keyId">Optional key ID to include in the header for key rotation. Default is an empty string.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="publicKey"/> is null or whitespace.</exception>
    /// <exception cref="FormatException">Thrown when <paramref name="publicKey"/> is in an invalid format.</exception>
    /// <exception cref="CryptographicException">Thrown when the format is invalid or cannot be parsed as an RSA public key.</exception>
    /// <remarks>
    /// The public key is loaded and validated during construction. Keep the corresponding private key secure
    /// for decryption purposes - it should never be deployed with application code.
    /// </remarks>
    /// <example>
    /// <code>
    /// // From configuration
    /// string publicKey = configuration["Logging:EncryptionPublicKey"];
    /// var hooks = new EncryptHooks(publicKey);
    ///
    /// // From file
    /// string publicKey = File.ReadAllText("public_key.(xml/pem)");
    /// var hooks = new EncryptHooks(publicKey);
    /// </code>
    /// </example>
    public EncryptHooks(string publicKey, string keyId = "")
    {
        ArgumentNullException.ThrowIfNull(publicKey);
        RSA rsa = _rsaCache.GetOrAdd(publicKey, CreateRsaFromString);
        _encryptionOptions = new EncryptionOptions(rsa, keyId);
    }

    private static RSA CreateRsaFromString(string publicKey)
    {
        var r = RSA.Create();
        r.FromString(publicKey);
        return r;
    }

    /// <summary>
    /// Called by Serilog.Sinks.File when a log file is opened. Wraps the stream with encryption.
    /// </summary>
    /// <param name="path">The path to the log file being opened.</param>
    /// <param name="underlyingStream">The underlying file stream created by Serilog.</param>
    /// <param name="encoding">The text encoding used for log entries.</param>
    /// <returns>An <see cref="LogWriter"/> that wraps the underlying stream for transparent encryption.</returns>
    /// <remarks>
    /// This method is called internally by Serilog and should not be called directly by application code.
    /// The returned stream is managed by Serilog and will be disposed when the log file is closed.
    /// </remarks>
    public override Stream OnFileOpened(string path, Stream underlyingStream, Encoding encoding)
    {
        return new LogWriter(underlyingStream, _encryptionOptions);
    }
}
