namespace Serilog.Sinks.File.Encrypt.Cli;

/// <summary>
/// Thrown when a passphrase source was specified but could not produce a usable passphrase
/// (missing file, unset environment variable, mismatched confirmation, ...). Commands map
/// this to the usage-error exit code.
/// </summary>
public sealed class PassphraseResolutionException(string message) : Exception(message);

/// <summary>
/// Resolves the private-key passphrase from the supported sources, in order:
/// <c>--passphrase-file</c> (first line of the file), <c>--passphrase-env</c> (named
/// environment variable), the <c>SERILOG_ENCRYPT_PASSPHRASE</c> environment variable, and
/// finally an interactive hidden prompt when the console supports it. There is deliberately
/// no command-line passphrase option — secrets on argv leak via shell history and process
/// listings.
/// </summary>
public interface IPassphraseResolver
{
    /// <summary>
    /// The name of the environment variable that is always checked as a fallback source.
    /// </summary>
    const string DefaultEnvironmentVariable = "SERILOG_ENCRYPT_PASSPHRASE";

    /// <summary>
    /// Resolves a passphrase from the configured sources.
    /// </summary>
    /// <param name="passphraseFile">Path of a file whose first line is the passphrase, or null.</param>
    /// <param name="passphraseEnv">Name of an environment variable holding the passphrase, or null.</param>
    /// <param name="confirm">
    /// When prompting interactively, ask for the passphrase twice and require both entries
    /// to match (used when creating a new key).
    /// </param>
    /// <returns>
    /// The resolved passphrase, or null when no source is configured and the console is
    /// not interactive — the caller decides whether that is an error.
    /// </returns>
    /// <exception cref="PassphraseResolutionException">
    /// A configured source could not produce a non-empty passphrase.
    /// </exception>
    string? Resolve(string? passphraseFile, string? passphraseEnv, bool confirm);
}
