// See https://aka.ms/new-console-template for more information
using Example.Console.Logging;
using Serilog;
using Serilog.Sinks.File.Encrypt;

Console.WriteLine("Hello, World!");

var l = new LoggingService();
string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
if (!Directory.Exists(logDirectory))
{
    Directory.CreateDirectory(logDirectory);
}

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: Path.Combine(logDirectory, "log-.txt"),
        rollingInterval: RollingInterval.Day,
        hooks: new DeviceEncryptHooks(l.PublicKey.ToXmlString(false)), // Use public key for encryption
        retainedFileCountLimit: 31 // Keep logs for 31 days
    )
    .CreateLogger();

Log.Information("Application started at {StartTime}", DateTimeOffset.Now);
Log.Debug("This is a debug message.");
Log.Warning("This is a warning message.");
Log.Error(new Exception("This is an error message."), "This is an error message.");
Log.Information("Application ended at {EndTime}", DateTimeOffset.Now);
await Log.CloseAndFlushAsync();
Console.WriteLine("Logs written to " + logDirectory);
Console.WriteLine("Press any key to exit...");
Console.ReadKey();
Console.WriteLine("Exiting...");
