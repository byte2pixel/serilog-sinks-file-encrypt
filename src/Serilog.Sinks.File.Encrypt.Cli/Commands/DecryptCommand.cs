using System.ComponentModel;
using System.IO.Abstractions;
using Serilog.Sinks.File.Encrypt;
using Spectre.Console.Cli;

namespace Serilog.Sinks.Field.Encrypt.Cli.Commands;

/// <summary>
/// Command to decrypt an encrypted log file using an RSA private key
/// </summary>
/// <param name="fileSystem">The file system</param>
public sealed class DecryptCommand(IFileSystem fileSystem) : Command<DecryptCommand.Settings>
{
    /// <summary>
    /// The settings for the DecryptCommand
    /// </summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>
        /// The path to the RSA private key file in XML format.
        /// </summary>
        [CommandOption("-k|--key <KEY>")]
        [Description("The file containing the RSA private key in XML format")]
        public string KeyFile { get; set; } = "privateKey.xml";

        /// <summary>
        /// The path to the encrypted log file to decrypt.
        /// </summary>
        [CommandOption("-f|--file <FILE>")]
        [Description("The encrypted log file to decrypt")]
        public string EncryptedFile { get; set; } = "log.encrypted.txt";

        /// <summary>
        /// The path where the decrypted log content will be saved.
        /// </summary>
        [CommandOption("-o|--output <OUTPUT>")]
        [Description("The output file for the decrypted log content")]
        public string OutputFile { get; set; } = "log.decrypted.txt";
    }

    /// <summary>
    /// Decrypts the specified encrypted log file using the provided RSA private key and writes the decrypted content to the output file.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="settings">The decypt settings</param>
    /// <returns></returns>
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
            Console.Error.WriteLine(
                $"Error: Encrypted file '{settings.EncryptedFile}' does not exist."
            );
            return 1;
        }

        EncryptionUtils.DecryptLogFileToFile(
            settings.EncryptedFile,
            rsaPrivateKey,
            settings.OutputFile
        );
        Console.WriteLine($"Decrypted log written to '{settings.OutputFile}'.");
        return 0;
    }
}
