using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Serilog.Sinks.File.Encrypt.Cli.Commands;

/// <summary>
/// Generates a new RSA public/private key pair and saves them to the specified output path.
/// </summary>
/// <param name="console">The ANSI console.</param>
/// <param name="fileSystem">The file system.</param>
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public sealed class GenerateCommand(IAnsiConsole console, IFileSystem fileSystem)
    : Command<GenerateCommand.Settings>
{
    /// <summary>
    /// The settings for the GenerateCommand.
    /// </summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>
        /// The output path to write the public/private key pair in XML format.
        /// </summary>
        [CommandOption("-o|--output <OUTPUT>", isRequired: true)]
        [Description("The output path to write the public/private key pair in XML format")]
        public string OutputPath { get; set; } = string.Empty;

        /// <summary>
        /// The size of the RSA key in bits.
        /// </summary>
        [CommandOption("-k|--key-size <KEY_SIZE>")]
        [Description("The size of the RSA key in bits (default: 2048)")]
        [DefaultValue(2048)]
        public int KeySize { get; set; } = 2048;
    }

    /// <summary>
    /// Generates a new RSA key pair and writes them to the specified output path.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="settings">The generate settings.</param>
    /// <returns></returns>
    public override int Execute(CommandContext context, Settings settings)
    {
        try
        {
            // Ensure output directory exists
            if (!fileSystem.Directory.Exists(settings.OutputPath))
            {
                fileSystem.Directory.CreateDirectory(settings.OutputPath);
                console.MarkupLineInterpolated(
                    $"[yellow]Created directory:[/] {settings.OutputPath}"
                );
            }

            // Generate the RSA key pair
            (string publicKey, string privateKey) keyPair = EncryptionUtils.GenerateRsaKeyPair(
                settings.KeySize
            );

            // Define file paths
            string privateKeyPath = fileSystem.Path.Combine(settings.OutputPath, "private_key.xml");
            string publicKeyPath = fileSystem.Path.Combine(settings.OutputPath, "public_key.xml");

            // Write keys to files
            fileSystem.File.WriteAllText(privateKeyPath, keyPair.privateKey);
            fileSystem.File.WriteAllText(publicKeyPath, keyPair.publicKey);

            // Output success message
            console.MarkupLine("[green]✓ Successfully generated RSA key pair![/]");
            console.WriteLine();
            console.MarkupLineInterpolated($"[red]Private Key:[/] {privateKeyPath}");
            console.MarkupLineInterpolated($"[yellow]Public Key:[/] {publicKeyPath}");
            console.WriteLine();
            console.MarkupLine("[yellow]⚠️  Keep your private key secure and never share it![/]");

            return 0;
        }
        catch (Exception ex)
        {
            console.MarkupLineInterpolated($"[red]Error generating key pair: {ex.Message}[/]");
            return 1;
        }
    }
}
