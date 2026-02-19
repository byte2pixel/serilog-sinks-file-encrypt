using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;

namespace Serilog.Sinks.File.Encrypt.Models;

/// <summary>
/// Decryption options for Serilog.Sinks.File.Encrypt decryption tools.
/// </summary>
public sealed record DecryptionOptions
{
    /// <summary>
    /// A dictionary of RSA private keys indexed by their corresponding key IDs. This allows for key rotation and supports multiple keys for decryption.
    /// </summary>
    public required Dictionary<string, string> DecryptionKeys { get; init; } = [];

    /// <summary>
    /// Defines how decryption errors should be handled.
    /// </summary>
    /// <remarks>
    /// See <see cref="Models.ErrorHandlingMode"/> for detailed descriptions of each mode.
    /// Default is <see cref="ErrorHandlingMode.Skip"/> which silently skips corrupted sections.
    /// </remarks>
    public ErrorHandlingMode ErrorHandlingMode { get; init; } = ErrorHandlingMode.Skip;

    /// <summary>
    /// Optional path to an audit log files where decryption errors and related events will be recorded.
    /// If not specified, no audit logging will occur.
    /// </summary>
    public string? AuditLogPath { get; init; }
};
