using System.ComponentModel;
using System.IO.Abstractions;
using Serilog.Sinks.File.Encrypt;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Serilog.Sinks.Field.Encrypt.Cli.Commands;

/// <summary>
/// Generates a new RSA public/private key pair and saves them to the specified output path.
/// </summary>
/// <param name="console">The ANSI console.</param>
/// <param name="fileSystem">The file system.</param>
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
    }

    /// <summary>
    /// Generates a new RSA key pair and writes them to the specified output path.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="settings">The generate settings.</param>
    /// <returns></returns>
    public override int Execute(CommandContext context, Settings settings)
    {
        (string publicKey, string privateKey) keyPair = EncryptionUtils.GenerateRsaKeyPair();
        string privateKeyPath = fileSystem.Path.Combine(settings.OutputPath, "private_key.xml");
        string publicKeyPath = fileSystem.Path.Combine(settings.OutputPath, "public_key.xml");
        fileSystem.File.WriteAllText(privateKeyPath, keyPair.privateKey);
        fileSystem.File.WriteAllText(publicKeyPath, keyPair.publicKey);
        console.MarkupLine("[green]Generated RSA key pair:[/]");
        console.MarkupLineInterpolated($"[red]Private Key:[/] {privateKeyPath}");
        console.MarkupLineInterpolated($"[yellow]Public Key:[/] {publicKeyPath}");
        return 0;
    }
}
