namespace Serilog.Sinks.File.Encrypt.Cli;

/// <summary>
/// Renders decrypt-run results: a per-file session table and a run summary table for
/// humans, and the versioned JSON payload for --json.
/// </summary>
public interface IDecryptReporter
{
    /// <summary>
    /// Writes the per-file session table (informational level).
    /// </summary>
    /// <param name="report">The file report to render.</param>
    void ReportSessions(FileReport report);

    /// <summary>
    /// Writes the run summary table and its warning lines.
    /// </summary>
    /// <param name="summary">The run summary to render.</param>
    void ReportSummary(RunSummary summary);

    /// <summary>
    /// Serializes the full run report to indented JSON (schemaVersion included).
    /// </summary>
    /// <param name="report">The run report to serialize.</param>
    /// <returns>The JSON text.</returns>
    string ToJson(DecryptRunReport report);
}
