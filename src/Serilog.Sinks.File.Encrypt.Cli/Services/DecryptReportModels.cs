using System.Text.Json.Serialization;

namespace Serilog.Sinks.File.Encrypt.Cli;

/// <summary>
/// The outcome category of a single input file in a decrypt run. Serialized as camelCase
/// strings in --json output.
/// </summary>
public enum FileOutcome
{
    /// <summary>The file decrypted (possibly with partial failures reported in the counts).</summary>
    Success,

    /// <summary>A runtime error (IO/crypto) failed the file.</summary>
    Failed,

    /// <summary>The file was refused: existing output without --force, or a path containment violation.</summary>
    Refused,

    /// <summary>Zero sessions and zero messages were decrypted (wrong key / not an encrypted log).</summary>
    NothingDecrypted,
}

/// <summary>
/// Per-session detail for the decrypt report, mirroring
/// <c>Serilog.Sinks.File.Decrypt.Models.SessionResult</c>.
/// </summary>
public sealed record SessionReport(
    int Index,
    int FormatVersion,
    string KeyId,
    string SealStatus,
    ulong? DeclaredFrameCount,
    int DecryptedMessages,
    int FailedMessages
);

/// <summary>
/// Per-file result of a decrypt run.
/// </summary>
public sealed record FileReport(
    string Input,
    string? Output,
    FileOutcome Outcome,
    int DecryptedSessions,
    int DecryptedMessages,
    int FailedHeaders,
    int FailedMessages,
    int ResyncAttempts,
    bool AllSessionsSealed,
    IReadOnlyList<SessionReport> Sessions,
    string? Error
);

/// <summary>
/// Aggregated counts for a decrypt run. <paramref name="AllSessionsSealed"/> is strict:
/// every session of every successful file is cryptographically verified as sealed — v1
/// sessions (seal <c>NotApplicable</c>) count as NOT sealed, matching the library's
/// <c>RequireSealed</c> semantics. Per-file <see cref="FileReport.AllSessionsSealed"/>
/// keeps the library's v1-tolerant meaning.
/// </summary>
public sealed record RunSummary(
    int Files,
    int Succeeded,
    int Failed,
    int Refused,
    int NothingDecrypted,
    int TotalMessages,
    int TotalResyncAttempts,
    bool AllSessionsSealed
);

/// <summary>
/// The complete machine-readable decrypt report emitted by <c>decrypt --json</c>.
/// <see cref="SchemaVersion"/> is incremented on breaking schema changes.
/// </summary>
public sealed record DecryptRunReport(
    int SchemaVersion,
    IReadOnlyList<FileReport> Files,
    RunSummary Summary,
    int ExitCode
)
{
    /// <summary>
    /// The current --json schema version.
    /// </summary>
    public const int CurrentSchemaVersion = 1;
}

/// <summary>
/// Source-generated JSON context for the --json report (camelCase, indented,
/// enums as camelCase strings, nulls omitted).
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true
)]
[JsonSerializable(typeof(DecryptRunReport))]
public sealed partial class DecryptReportJsonContext : JsonSerializerContext;
