using Microsoft.Extensions.DependencyInjection;
using Serilog.Sinks.Field.Encrypt.Cli;
using Serilog.Sinks.Field.Encrypt.Cli.Commands;
using Serilog.Sinks.Field.Encrypt.Cli.Infrastructure;
using Spectre.Console.Cli;

var registrations = new ServiceCollection();
registrations.AddSingleton<IGreeter, HelloWorldGreeter>();

var registrar = new TypeRegistrar(registrations);
var app = new CommandApp(registrar);
app.Configure(c =>
{
    c.AddCommand<DecryptCommand>("decrypt");
});
return await app.RunAsync(args);
