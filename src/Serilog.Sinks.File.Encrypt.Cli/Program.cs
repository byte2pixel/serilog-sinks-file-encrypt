using Serilog.Sinks.File.Encrypt.Cli.Infrastructure;
using Spectre.Console.Cli;

TypeRegistrar registrar = CommandAppConfiguration.CreateRegistrar();
CommandApp app = new(registrar);

app.Configure(CommandAppConfiguration.GetConfiguration());

return await app.RunAsync(args);
