namespace Serilog.Sinks.File.Decrypt.Models;

/// <summary>
/// Per-session outcome of a decryption run. A log file contains one session per file open
/// (rolling files and application restarts append new sessions), and each session is
/// independently keyed and independently sealed.
/// </summary>
public sealed record SessionResult
{
    /// <summary>
    /// Zero-based order in which the session was encountered in the input stream.
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// The on-disk format version of the session header (1 or 2).
    /// </summary>
    public required byte FormatVersion { get; init; }

    /// <summary>
    /// The key ID from the session header identifying which RSA key encrypted the session.
    /// Empty when no key ID was configured.
    /// </summary>
    public string KeyId { get; init; } = "";

    /// <summary>
    /// The end-of-log seal verification status. See <see cref="Models.SealStatus"/> for the
    /// meaning of each value.
    /// </summary>
    public required SealStatus SealStatus { get; init; }

    /// <summary>
    /// The frame count declared by the session's seal record. Only present when the seal was
    /// found and authenticated (<see cref="Models.SealStatus.Sealed"/> or
    /// <see cref="Models.SealStatus.SealCountMismatch"/>).
    /// </summary>
    public ulong? DeclaredFrameCount { get; init; }

    /// <summary>
    /// Number of messages successfully decrypted from this session.
    /// </summary>
    public int DecryptedMessages { get; init; }

    /// <summary>
    /// Number of message decryption failures attributed to this session.
    /// </summary>
    public int FailedMessages { get; init; }
}
