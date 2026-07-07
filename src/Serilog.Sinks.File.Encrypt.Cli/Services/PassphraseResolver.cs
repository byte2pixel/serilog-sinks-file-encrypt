using System.IO.Abstractions;
using Spectre.Console;

namespace Serilog.Sinks.File.Encrypt.Cli;

/// <inheritdoc />
public sealed class PassphraseResolver(IAnsiConsole console, IFileSystem fileSystem)
    : IPassphraseResolver
{
    /// <inheritdoc />
    public string? Resolve(string? passphraseFile, string? passphraseEnv, bool confirm)
    {
        if (!string.IsNullOrWhiteSpace(passphraseFile))
        {
            return FromFile(passphraseFile);
        }

        if (!string.IsNullOrWhiteSpace(passphraseEnv))
        {
            return FromEnvironment(passphraseEnv);
        }

        string? fallback = Environment.GetEnvironmentVariable(
            IPassphraseResolver.DefaultEnvironmentVariable
        );
        if (!string.IsNullOrEmpty(fallback))
        {
            return fallback;
        }

        if (console.Profile.Capabilities.Interactive)
        {
            return FromPrompt(confirm);
        }

        return null;
    }

    private string FromFile(string path)
    {
        if (!fileSystem.File.Exists(path))
        {
            throw new PassphraseResolutionException($"Passphrase file '{path}' does not exist.");
        }

        string? firstLine = fileSystem.File.ReadLines(path).FirstOrDefault();
        if (string.IsNullOrEmpty(firstLine))
        {
            throw new PassphraseResolutionException(
                $"Passphrase file '{path}' is empty; the first line must contain the passphrase."
            );
        }

        return firstLine;
    }

    private static string FromEnvironment(string variableName)
    {
        string? value = Environment.GetEnvironmentVariable(variableName);
        if (string.IsNullOrEmpty(value))
        {
            throw new PassphraseResolutionException(
                $"Environment variable '{variableName}' is not set or empty."
            );
        }

        return value;
    }

    private string FromPrompt(bool confirm)
    {
        string passphrase = console.Prompt(new TextPrompt<string>("Enter passphrase:").Secret());
        if (string.IsNullOrEmpty(passphrase))
        {
            throw new PassphraseResolutionException("Passphrase must not be empty.");
        }

        if (confirm)
        {
            string confirmation = console.Prompt(
                new TextPrompt<string>("Confirm passphrase:").Secret()
            );
            if (!string.Equals(passphrase, confirmation, StringComparison.Ordinal))
            {
                throw new PassphraseResolutionException("Passphrases do not match.");
            }
        }

        return passphrase;
    }
}
