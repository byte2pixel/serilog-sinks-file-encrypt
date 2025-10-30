using System.ComponentModel;
using System.IO.Abstractions;
using Serilog.Sinks.File.Encrypt;
using Spectre.Console.Cli;

namespace Serilog.Sinks.Field.Encrypt.Cli.Commands;

public sealed class DecryptCommand(IFileSystem fileSystem) : Command<DecryptCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-k|--key <KEY>")]
        [Description("The file containing the RSA private key in XML format")]
        public string KeyFile { get; set; } = "privateKey.xml";
        [CommandOption("-f|--file <FILE>")]
        [Description("The encrypted log file to decrypt")]
        public string EncryptedFile { get; set; } = "log.encrypted.txt";
        [CommandOption("-o|--output <OUTPUT>")]
        [Description("The output file for the decrypted log content")]
        public string OutputFile { get; set; } = "log.decrypted.txt";
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        // Read the RSA private key from the specified file
        if (!fileSystem.File.Exists(settings.KeyFile))
        {
            Console.Error.WriteLine($"Error: Key file '{settings.KeyFile}' does not exist.");
            return 1;
        }
        string rsaPrivateKey = fileSystem.File.ReadAllText(settings.KeyFile);
        if (!fileSystem.File.Exists(settings.EncryptedFile))
        {
            Console.Error.WriteLine($"Error: Encrypted file '{settings.EncryptedFile}' does not exist.");
            return 1;
        }
        EncryptionUtils.DecryptLogFileToFile(settings.EncryptedFile, settings.OutputFile, rsaPrivateKey);
        return 0;
    }
}
