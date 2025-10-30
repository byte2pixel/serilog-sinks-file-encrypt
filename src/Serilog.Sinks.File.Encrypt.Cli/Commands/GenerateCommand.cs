using System.ComponentModel;
using System.IO.Abstractions;
using Serilog.Sinks.File.Encrypt;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Serilog.Sinks.Field.Encrypt.Cli.Commands;

public sealed class GenerateCommand(IAnsiConsole console, IFileSystem fileSystem) : Command<GenerateCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-o|--output <OUTPUT>", isRequired: true)]
        [Description("The output path to write the public/private key pair in XML format")]
        public string OutputPath { get; set; } = string.Empty;
    }

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
