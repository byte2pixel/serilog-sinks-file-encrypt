using Microsoft.Extensions.DependencyInjection;
using Serilog.Sinks.Field.Encrypt.Cli;
using Serilog.Sinks.Field.Encrypt.Cli.Commands;
using Serilog.Sinks.Field.Encrypt.Cli.Infrastructure;
using Spectre.Console.Cli;

ServiceCollection registrations = new();
// DI Registrations
// e.g. registrations.AddSingleton<,>();

TypeRegistrar registrar = new(registrations);
CommandApp app = new(registrar);

app.Configure(c =>
{
    c.AddCommand<DecryptCommand>("decrypt");
});

return await app.RunAsync(args);
