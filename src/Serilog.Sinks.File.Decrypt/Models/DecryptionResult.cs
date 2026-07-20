namespace Serilog.Sinks.File.Decrypt.Models;

/// <summary>
/// The result of a decryption operation, containing counts of decrypted sessions, messages, failures, and resync
/// attempts, plus per-session detail (including end-of-log seal verification) in <see cref="Sessions"/>.
/// </summary>
public sealed class DecryptionResult
{
    /// <summary>
    /// Number of successfully decrypted sessions.
    /// A session is defined as a complete set of log messages that were encrypted together.
    /// </summary>
    public int DecryptedSessions { get; init; }

    /// <summary>
    /// Number of successfully decrypted messages.
    /// This counts individual log messages that were successfully decrypted, regardless of session boundaries.
    /// </summary>
    public int DecryptedMessages { get; init; }

    /// <summary>
    /// The number of decryption errors encountered while processing what appears
    /// to be a session header. This indicates issues with the session metadata,
    /// such as missing or corrupted headers that prevent identifying the session's
    /// decryption parameters.
    /// </summary>
    public int FailedHeaders { get; init; }

    /// <summary>
    /// The number of decryption errors encountered while processing individual log messages.
    /// This indicates issues with the message content, such as corruption or decryption failures
    /// that occur after successfully identifying the session. These errors may be due to data corruption,
    /// incorrect decryption keys, or other issues that prevent successful decryption of the message content
    /// </summary>
    public int FailedMessages { get; init; }

    /// <summary>
    /// <para>
    /// The number of times the decryption process had to attempt resynchronization after encountering errors.
    /// Resynchronization attempts to occur when the decryption process detects a corrupted section and tries to skip
    /// past it to continue processing subsequent messages.
    /// </para>
    /// <para>
    /// A high number of resynchronization attempts may indicate significant corruption in the log file,
    /// while a low number suggests that most of the file was successfully decrypted with minimal issues.
    /// </para>
    /// </summary>
    public int ResyncAttempts { get; init; }

    /// <summary>
    /// Per-session results in the order the sessions were encountered in the input, including
    /// each session's format version and end-of-log seal status. The flat counters above are
    /// file-level aggregates and additionally cover failures that cannot be attributed to any
    /// session (e.g. corrupted headers found during resync).
    /// </summary>
    public IReadOnlyList<SessionResult> Sessions { get; init; } = [];

    /// <summary>
    /// Number of sessions whose completeness could not be positively verified: unsealed sessions
    /// (crash or truncation), seal count mismatches (truncation of a cleanly closed log), and
    /// invalid seals (tampering). v1 sessions (<see cref="SealStatus.NotApplicable"/>) are not
    /// counted here; use <see cref="AllSessionsSealed"/> for a strict check.
    /// </summary>
    public int UnsealedSessions =>
        Sessions.Count(s =>
            s.SealStatus
                is SealStatus.Unsealed
                    or SealStatus.SealCountMismatch
                    or SealStatus.SealInvalid
        );

    /// <summary>
    /// True when at least one session was found and every session either verified as sealed
    /// or predates seal support (v1). False when the result contains no sessions at all —
    /// an empty result carries no seal evidence (see <see cref="NothingDecrypted"/>).
    /// </summary>
    public bool AllSessionsSealed =>
        Sessions.Count > 0
        && Sessions.All(s => s.SealStatus is SealStatus.Sealed or SealStatus.NotApplicable);

    /// <summary>
    /// <para>
    /// True when the operation produced no output at all: zero decrypted sessions and zero
    /// decrypted messages. In <see cref="ErrorHandlingMode.Skip"/> mode this is the signal
    /// that nothing in the input could be read — the output stream is empty.
    /// </para>
    /// <para>
    /// Combined with <see cref="FailedHeaders"/> &gt; 0 this strongly indicates a wrong
    /// decryption key, a wrong key id, or a file that is not an encrypted log; with zero
    /// recorded failures it typically means the input contained no recognizable session at
    /// all (e.g. an empty or foreign file).
    /// </para>
    /// </summary>
    public bool NothingDecrypted => DecryptedSessions == 0 && DecryptedMessages == 0;
}
