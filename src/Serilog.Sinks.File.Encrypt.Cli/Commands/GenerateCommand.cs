using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using Serilog.Sinks.File.Encrypt.Cli.Infrastructure;
using Serilog.Sinks.File.Encrypt.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Serilog.Sinks.File.Encrypt.Cli.Commands;

/// <summary>
/// Generates a new RSA public/private key pair and saves them to the specified output path.
/// By default the private key is passphrase-encrypted (PKCS#8 PEM); pass --plaintext for a
/// legacy unencrypted key.
/// </summary>
/// <param name="writer">The verbosity-aware console writer.</param>
/// <param name="fileSystem">The file system.</param>
/// <param name="passphraseResolver">Resolves the private-key passphrase from file, environment, or prompt.</param>
/// <param name="keyFileWriter">Writes key files, restricting the private key to the current user.</param>
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public sealed class GenerateCommand(
    IConsoleWriter writer,
    IFileSystem fileSystem,
    IPassphraseResolver passphraseResolver,
    IKeyFileWriter keyFileWriter
) : Command<GenerateCommand.Settings>
{
    /// <summary>
    /// The settings for the GenerateCommand.
    /// </summary>
    public sealed class Settings : GlobalCommandSettings
    {
        /// <summary>
        /// The output path to write the public/private key pair.
        /// </summary>
        [CommandOption("-o|--output <OUTPUT>", isRequired: true)]
        [Description("The output path to write the public/private key pair")]
        public string OutputPath { get; init; } = string.Empty;

        /// <summary>
        /// The size of the RSA key in bits.
        /// </summary>
        [CommandOption("--key-size <KEY_SIZE>")]
        [Description("The size of the RSA key in bits (default: 3072)")]
        [DefaultValue(3072)]
        public int KeySize { get; init; } = 3072;

        /// <summary>
        /// The export format for the RSA keys. Xml is legacy: it has no encrypted
        /// representation and requires --plaintext.
        /// </summary>
        [CommandOption("-f|--format <FORMAT>")]
        [Description(
            "The encoding format (Pem or Xml) for the RSA keys (default: Pem). Xml is legacy and requires --plaintext."
        )]
        public KeyFormat Format { get; init; } = KeyFormat.Pem;

        /// <summary>
        /// Name of an environment variable holding the private-key passphrase.
        /// </summary>
        [CommandOption("--passphrase-env <NAME>")]
        [Description("Read the private-key passphrase from the named environment variable")]
        public string? PassphraseEnv { get; init; }

        /// <summary>
        /// Path of a file whose first line is the private-key passphrase.
        /// </summary>
        [CommandOption("--passphrase-file <PATH>")]
        [Description("Read the private-key passphrase from the first line of the given file")]
        public string? PassphraseFile { get; init; }

        /// <summary>
        /// Write the private key unencrypted. Required for --format Xml. Without a
        /// passphrase source, an interactive prompt is used; in a non-interactive session
        /// the command fails unless this flag is set.
        /// </summary>
        [CommandOption("--plaintext")]
        [Description(
            "Write the private key unencrypted (required for Xml format). Default is a passphrase-encrypted PKCS#8 private key."
        )]
        [DefaultValue(false)]
        public bool Plaintext { get; init; }

        /// <summary>
        /// Overwrite existing key files. Without this flag, generation is refused when a
        /// key file already exists — overwriting a private key permanently loses access to
        /// all logs encrypted with it.
        /// </summary>
        [CommandOption("--force")]
        [Description("Overwrite existing key files (default: refuse if key files exist)")]
        [DefaultValue(false)]
        public bool Force { get; init; }

        /// <inheritdoc />
        public override ValidationResult Validate()
        {
            if (Format == KeyFormat.Xml && !Plaintext)
            {
                return ValidationResult.Error(
                    "✗ Error: the Xml format cannot store an encrypted private key. Pass --plaintext to generate a legacy unencrypted Xml key, or use the Pem format."
                );
            }

            if (Plaintext && (PassphraseEnv is not null || PassphraseFile is not null))
            {
                return ValidationResult.Error(
                    "✗ Error: --plaintext cannot be combined with a passphrase source."
                );
            }

            return base.Validate();
        }
    }

    /// <summary>
    /// Generates a new RSA key pair and writes them to the specified output path.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="settings">The generate settings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    public override int Execute(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken
    )
    {
        writer.Verbosity = settings.Verbosity;
        try
        {
            // Ensure output directory exists
            if (!fileSystem.Directory.Exists(settings.OutputPath))
            {
                fileSystem.Directory.CreateDirectory(settings.OutputPath);
                writer.Info($"[yellow]Created directory:[/] {settings.OutputPath}");
            }

            string fileExt = settings.Format.ToString().ToLower();

            // Define file paths
            string privateKeyPath = fileSystem.Path.Join(
                settings.OutputPath,
                $"private_key.{fileExt}"
            );
            string publicKeyPath = fileSystem.Path.Join(
                settings.OutputPath,
                $"public_key.{fileExt}"
            );

            if (!settings.Force)
            {
                string[] existing = new[] { privateKeyPath, publicKeyPath }
                    .Where(fileSystem.File.Exists)
                    .ToArray();
                if (existing.Length > 0)
                {
                    writer.Error(
                        $"[red]✗ Refused: key file(s) already exist: {string.Join(", ", existing)}[/]"
                    );
                    writer.Warning(
                        $"[yellow]Use --force to overwrite. Overwriting a private key permanently loses access to all logs encrypted with it.[/]"
                    );
                    return ExitCodes.UsageError;
                }
            }

            string? passphrase = null;
            if (!settings.Plaintext)
            {
                passphrase = passphraseResolver.Resolve(
                    settings.PassphraseFile,
                    settings.PassphraseEnv,
                    confirm: true
                );
                if (passphrase is null)
                {
                    writer.Error(
                        $"[red]✗ No passphrase source available in a non-interactive session.[/]"
                    );
                    writer.Warning(
                        $"[yellow]Provide --passphrase-env or --passphrase-file (or set {IPassphraseResolver.DefaultEnvironmentVariable}), or pass --plaintext to write an unencrypted private key.[/]"
                    );
                    return ExitCodes.UsageError;
                }
            }

            // Generate the RSA key pair
            (string publicKey, string privateKey) keyPair = passphrase is null
                ? CryptographicUtils.GenerateRsaKeyPair(settings.KeySize, settings.Format)
                : CryptographicUtils.GenerateRsaKeyPair(
                    settings.KeySize,
                    settings.Format,
                    passphrase
                );

            // Write keys to files; the private key is restricted to the current user
            keyFileWriter.WritePrivateKey(privateKeyPath, keyPair.privateKey);
            keyFileWriter.WritePublicKey(publicKeyPath, keyPair.publicKey);

            // Output success message
            writer.Info($"[green]✓ Successfully generated RSA key pair![/]");
            writer.BlankLine();
            writer.Info($"[red]Private Key:[/] {privateKeyPath}");
            writer.Info($"[yellow]Public Key:[/] {publicKeyPath}");
            writer.BlankLine();
            if (passphrase is null)
            {
                writer.Warning(
                    $"[yellow]⚠️  The private key is NOT passphrase-protected. Keep it secure and never share it![/]"
                );
            }
            else
            {
                writer.Info(
                    $"[green]The private key is passphrase-encrypted (PKCS#8).[/] [yellow]There is no recovery if the passphrase is lost.[/]"
                );
                writer.Warning($"[yellow]⚠️  Keep your private key secure and never share it![/]");
            }

            return ExitCodes.Success;
        }
        catch (PassphraseResolutionException ex)
        {
            writer.Error($"[red]✗ {ex.Message}[/]");
            return ExitCodes.UsageError;
        }
        catch (IOException ex)
        {
            writer.Error($"[red]Error writing key files: {ex.Message}[/]");
            return ExitCodes.RuntimeFailure;
        }
        catch (UnauthorizedAccessException ex)
        {
            writer.Error($"[red]Access denied to output path: {ex.Message}[/]");
            return ExitCodes.RuntimeFailure;
        }
        catch (System.Security.Cryptography.CryptographicException ex)
        {
            writer.Error($"[red]Error generating RSA key pair: {ex.Message}[/]");
            return ExitCodes.RuntimeFailure;
        }
    }
}
