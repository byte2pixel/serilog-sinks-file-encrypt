namespace Serilog.Sinks.File.Encrypt.Cli.Infrastructure;

/// <summary>
/// The process exit-code contract for the serilog-encrypt CLI. Scripts can rely on these
/// values to distinguish failure modes without parsing console output.
/// When several conditions apply across a multi-file run, the highest-priority code wins:
/// <see cref="RuntimeFailure"/> &gt; <see cref="NothingDecrypted"/> &gt; <see cref="NotSealed"/>
/// &gt; <see cref="NoFilesMatched"/>.
/// </summary>
public static class ExitCodes
{
    /// <summary>
    /// All matched files were decrypted, or the key pair was generated.
    /// </summary>
    public const int Success = 0;

    /// <summary>
    /// At least one file failed with a runtime error (IO, cryptography, access denied,
    /// or an unexpected exception).
    /// </summary>
    public const int RuntimeFailure = 1;

    /// <summary>
    /// The invocation itself was invalid: bad arguments, a missing key file, a refused
    /// overwrite without --force, or a missing passphrase in a non-interactive session.
    /// Spectre.Console.Cli parse and validation failures are normalized to this value.
    /// </summary>
    public const int UsageError = 2;

    /// <summary>
    /// No input files matched the given path or glob pattern.
    /// </summary>
    public const int NoFilesMatched = 3;

    /// <summary>
    /// A file produced no decrypted sessions and no messages — typically a wrong key,
    /// a wrong key id, or a file that is not an encrypted log (#84).
    /// </summary>
    public const int NothingDecrypted = 4;

    /// <summary>
    /// --require-sealed was set and at least one session was not cryptographically
    /// verified as sealed.
    /// </summary>
    public const int NotSealed = 5;

    /// <summary>
    /// Maps a raw Spectre.Console.Cli result to the documented contract: parse and
    /// validation failures surface as negative values and become
    /// <see cref="UsageError"/>; everything else passes through unchanged.
    /// </summary>
    /// <param name="spectreExitCode">The value returned by CommandApp.Run/RunAsync.</param>
    /// <returns>The process exit code to return to the shell.</returns>
    public static int Normalize(int spectreExitCode) =>
        spectreExitCode < 0 ? UsageError : spectreExitCode;
}
