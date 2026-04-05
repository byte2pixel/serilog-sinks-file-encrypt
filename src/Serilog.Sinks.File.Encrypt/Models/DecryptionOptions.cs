using Serilog.Sinks.File.Encrypt.Interfaces;

namespace Serilog.Sinks.File.Encrypt.Models;

/// <summary>
/// Decryption options for Serilog.Sinks.File.Encrypt decryption tools.
/// </summary>
public sealed record DecryptionOptions
{
    /// <summary>
    /// The <see cref="IKeyProvider"/> implementation responsible for providing the decryption of the AES Session
    /// key and nonce used to encrypt the log entry(ies). This is a required property and must be set for decryption to work.
    /// </summary>
    public required IKeyProvider KeyProvider { get; init; }

    /// <summary>
    /// Defines how decryption errors should be handled.
    /// </summary>
    /// <remarks>
    /// See <see cref="Models.ErrorHandlingMode"/> for detailed descriptions of each mode.
    /// Default is <see cref="ErrorHandlingMode.Skip"/> which silently skips corrupted sections.
    /// </remarks>
    public ErrorHandlingMode ErrorHandlingMode { get; init; } = ErrorHandlingMode.Skip;
};
