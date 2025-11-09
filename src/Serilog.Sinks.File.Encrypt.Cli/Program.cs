using System.IO.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Sinks.Field.Encrypt.Cli.Commands;
using Serilog.Sinks.Field.Encrypt.Cli.Infrastructure;
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
    c.AddCommand<GenerateCommand>("generate");
    c.AddCommand<DecryptCommand>("decrypt");
});

return await app.RunAsync(args);
