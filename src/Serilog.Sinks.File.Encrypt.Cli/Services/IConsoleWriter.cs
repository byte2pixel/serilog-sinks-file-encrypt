namespace Serilog.Sinks.File.Encrypt.Cli;

/// <summary>
/// The output verbosity of a command run.
/// </summary>
public enum Verbosity
{
    /// <summary>Only warnings and errors are written.</summary>
    Quiet,

    /// <summary>Informational output plus warnings and errors (the default).</summary>
    Normal,

    /// <summary>Everything, including diagnostic detail.</summary>
    Verbose,
}

/// <summary>
/// Writes human-facing console output at a level, so commands honor --quiet/--verbose
/// consistently. Warnings and errors are always written; informational output is dropped
/// at <see cref="Cli.Verbosity.Quiet"/> and diagnostic output only appears at
/// <see cref="Cli.Verbosity.Verbose"/>. Interpolated values are markup-escaped; markup in
/// the literal text is honored.
/// </summary>
public interface IConsoleWriter
{
    /// <summary>
    /// The verbosity for the current run. Commands set this from their settings before
    /// writing any output.
    /// </summary>
    Verbosity Verbosity { get; set; }

    /// <summary>
    /// Writes an informational line. Dropped when <see cref="Verbosity"/> is Quiet.
    /// </summary>
    /// <param name="markup">The markup text to write.</param>
    void Info(FormattableString markup);

    /// <summary>
    /// Writes a diagnostic line. Only written when <see cref="Verbosity"/> is Verbose.
    /// </summary>
    /// <param name="markup">The markup text to write.</param>
    void Verbose(FormattableString markup);

    /// <summary>
    /// Writes a warning line. Always written.
    /// </summary>
    /// <param name="markup">The markup text to write.</param>
    void Warning(FormattableString markup);

    /// <summary>
    /// Writes an error line. Always written.
    /// </summary>
    /// <param name="markup">The markup text to write.</param>
    void Error(FormattableString markup);

    /// <summary>
    /// Writes a blank spacer line. Dropped when <see cref="Verbosity"/> is Quiet.
    /// </summary>
    void BlankLine();
}
