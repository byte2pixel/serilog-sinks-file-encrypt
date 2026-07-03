using Serilog.Sinks.File.Decrypt.Interfaces;

namespace Serilog.Sinks.File.Decrypt.Models;

/// <summary>
/// Decryption options for Serilog.Sinks.File.Decrypt decryption tools.
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

    /// <summary>
    /// Treat any session that is not cryptographically verified as sealed as an error.
    /// This includes unsealed v2 sessions (crash or truncation), seal mismatches, and all
    /// v1-format sessions (which have no seal support).
    /// </summary>
    /// <remarks>
    /// Interaction with <see cref="ErrorHandlingMode"/>:
    /// <list type="bullet">
    /// <item><see cref="ErrorHandlingMode.Skip"/>: non-sealed sessions are never fatal; they are
    /// reported via <see cref="DecryptionResult.Sessions"/> and
    /// <see cref="DecryptionResult.UnsealedSessions"/> regardless of this flag.</item>
    /// <item><see cref="ErrorHandlingMode.ThrowException"/>: when this flag is set, a
    /// <see cref="System.Security.Cryptography.CryptographicException"/> is thrown as soon as a
    /// session finishes without verifying as sealed.</item>
    /// </list>
    /// Leave this off (default) for crash-tolerant reading: a log whose writer crashed is
    /// indistinguishable from a truncated one and still decrypts fully.
    /// </remarks>
    public bool RequireSealed { get; init; } = false;
};
