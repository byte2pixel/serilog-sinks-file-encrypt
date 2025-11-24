using System.IO.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Sinks.File.Encrypt.Cli.Commands;
using Serilog.Sinks.File.Encrypt.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

ServiceCollection registrations = new();

// DI Registrations
registrations.AddTransient<IFileSystem>(_ => new FileSystem());
registrations.AddSingleton(AnsiConsole.Console);

TypeRegistrar registrar = new(registrations);
CommandApp app = new(registrar);

app.Configure(c =>
{
    c.SetApplicationName("serilog-encrypt");
    c.AddCommand<GenerateCommand>("generate")
        .WithDescription("Generate a new RSA key pair for log encryption");
    c.AddCommand<DecryptCommand>("decrypt")
        .WithDescription("Decrypt encrypted log files using an RSA private key");
    c.ValidateExamples();
});

return await app.RunAsync(args);
