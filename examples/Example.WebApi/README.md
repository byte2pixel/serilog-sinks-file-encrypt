# Example.WebApi

This example demonstrates how to set up Serilog logging in an ASP.NET Core Web API project with environment-specific configurations that support encrypted logging in production.

## Overview

This example showcases a real-world scenario where:
- **Development environment**: Logs to console and unencrypted file for easy debugging
- **Production environment**: Logs only to encrypted files for security

The key insight is that ASP.NET Core automatically merges `appsettings.json` with environment-specific files like `appsettings.Production.json`. The encryption hook is only configured in the Production settings, so it only runs in production.

## How It Works

1. **Configuration Merging**: ASP.NET Core automatically loads `appsettings.json` first, then overlays `appsettings.{Environment}.json`
2. **Environment Detection**: The `EncryptSetup` class reads `ASPNETCORE_ENVIRONMENT` to determine which configuration files to load
3. **Conditional Encryption**: The encryption hook (`SetupHook`) is only specified in `appsettings.Production.json`, so encryption only occurs in production
4. **Key Management**: The RSA public key is stored in configuration and read by the encryption setup class

## Setup Instructions


Create a new ASP.NET Core Web API project if you don't have one already:
```bash
dotnet new webapi -n Example.WebApi
cd Example.WebApi
```

Add the necessary Serilog packages:
```bash
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.Console
dotnet add package Serilog.Sinks.File
dotnet add package Serilog.Sinks.File.Encrypt
dotnet add package Serilog.Enrichers.Thread
```

Generate a key pair using the CLI tool:
```bash
# Install tool if needed
dotnet tool install --global Serilog.Sinks.File.Encrypt.Cli

# Generate key pair
serilog-encrypt generate -o .
```

Open the appsettings.json and configure the common Serilog settings by adding:
```json
{
  "Serilog": {
    "Enrich": [ "FromLogContext", "WithThreadId" ]
  }
}
```

Configure the Production environment to log only to an ecrypted file by creating an `appsettings.Production.json`:
Take note of the `hooks` parameter in the File sink configuration, this points to the static method that sets up the encryption hook.
Also note the `LogPublicKey` setting that contains the RSA public key used for encryption.  The static method will read this setting from configuration.
```json
{
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "logs/prod.txt",
          "rollingInterval": "Day",
          "hooks": "Example.WebApi.EncryptSetup::SetupHook, Example.WebApi",
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] <{ThreadId}>  {Message:lj} {Properties}{NewLine}{Exception}"
        }
      }
    ]
  },
  "LogPublicKey": "<RSAKeyValue><Modulus>ym5lM38zTPH+SF/RZBeNTo30tvEwt0KjA11oMnn9ADlCt7ymKf46dzA8A1q2dza8AfB15SU4KIiqJLic0qaGfMo8d8008HB2b+FpepuTfSyjR6iJCZpMaoJNtrlM0IRgSslP5CuSIL6jvk/3JuIfzxwYHn6m2IfvRTI6tLH5hxwXmxLAaWHlGJo9Sml3vcA2eA2CMBWacm/heogb0VAqeazYp1+Vv3Zul3nQEVttiPmfQbBHyqnNXfdCpm9yCE6sAzjTTWVk+XWoF3WZKVzA/vi6hkxUCEVgK65kr2UV+bF3QJOQGlrB7AytpNEfonG5wM72lNyPjWxHp8O6hON3cQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>"
}
```

Configure the development environment to log to the console and file (un-encrypted) by creating an `appsettings.Development.json`:
```json
{
  "Serilog": {
    "MinimumLevel": "Debug",
    "WriteTo": [
      {
        "Name": "Console"
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/debug.txt",
          "rollingInterval": "Day",
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] <{ThreadId}>  {Message:lj} {Properties}{NewLine}{Exception}"
        }
      }
    ]
  }
}
```

Create a new class `EncryptSetup.cs` to set up the encryption hook. This is called by the Serilog File sink during configuration:

**Important**: This class is only invoked when the `hooks` parameter is present in the configuration. Since the hook is only configured in `appsettings.Production.json`, it only runs in production.

```csharp
using Serilog.Sinks.File.Encrypt;

namespace Example.WebApi;

public static class EncryptSetup
{
    public static EncryptHooks SetupHook
    {
        get
        {
            string environment =
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile($"appsettings.{environment}.json", optional: true)
                .Build();

            string? rsaPublicKey = configuration["LogPublicKey"];

            return string.IsNullOrWhiteSpace(rsaPublicKey)
                ? throw new InvalidOperationException("LogPublicKey not found in configuration")
                : new EncryptHooks(rsaPublicKey);
        }
    }
}
```

**Why this works**: The hook method manually loads configuration because it's called during Serilog initialization, before the main application configuration is fully available. It needs to read the same configuration files to get the encryption key.

Update the `Program.cs` to use Serilog for logging:
```csharp
using System.Diagnostics;
using Example.WebApi;
using Microsoft.AspNetCore.Mvc;
using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();

builder.Services.AddSerilog(
    (context, configuration) =>
    {
        configuration.ReadFrom.Configuration(builder.Configuration).ReadFrom.Services(context);
    }
);

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// existing code...

await app.RunAsync();
```

Create a `WeatherForecast.cs` record for the API:
```csharp
namespace Example.WebApi;

public record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
```

## Setting Up Launch Profiles (Optional)

For easier testing in the IDE, you can configure launch profiles in `Properties/launchSettings.json`:

```json
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "https": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": false,
      "applicationUrl": "https://localhost:7161;http://localhost:5077",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    },
    "Production": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": false,
      "applicationUrl": "https://localhost:7161;http://localhost:5077",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Production"
      }
    }
  }
}
```

This allows you to easily switch between Development and Production configurations from your IDE.

Modify the api endpoint to inject an `ILogger` and log a message:
```csharp
app.MapGet(
        "/weatherforecast",
        ([FromServices] ILogger<Program> logger) =>
        {
            logger.LogDebug("Weather forecast requested");
            // existing code to generate forecast
            logger.LogInformation("Weather forecast generated");
            return forecast;
        }
    )
    .WithName("GetWeatherForecast");
```

## Validating Your Setup

### Quick Test
To verify everything is working correctly:

1. **Build the project**:
   ```bash
   dotnet build
   ```
   Should complete without errors.

2. **Test Development environment**:
   ```bash
   dotnet run --launch-profile https
   ```
   - Check console output for log messages
   - Check `logs/debug{date}.txt` for unencrypted log entries
   - Call the API: `curl https://localhost:7161/weatherforecast`

3. **Test Production environment**:
   ```bash
   dotnet run --launch-profile Production
   ```
   - No console output (logging disabled in Production)
   - Check `logs/prod{date}.txt` for encrypted log entries
   - Call the API: `curl https://localhost:7161/weatherforecast`

4. **Verify encryption is working**:
   ```bash
   # Open the production log file in a text editor
   # You should see encrypted/binary content, not readable text
   notepad logs/prod{date}.txt
   
   # Decrypt to verify content
   serilog-encrypt decrypt -i logs/prod{date}.txt -o logs/decrypted.txt -k private_key.xml
   notepad logs/decrypted.txt
   ```

## Running the Application

### Development Environment
Run the application in Development environment:
```bash
dotnet run --environment Development
# OR using launch profile from IDE
dotnet run --launch-profile https
```
**Expected behavior**:
- Logs appear in the console
- Logs are written to `logs/debug.txt` (unencrypted)
- No encryption hook is called (since hooks parameter is not in Development config)

### Production Environment
Run the application in Production environment:
```bash
dotnet run --environment Production
# OR using launch profile from IDE
dotnet run --launch-profile Production
```
**Expected behavior**:
- No console logging
- Logs are written to `logs/prod.txt` (encrypted)
- Encryption hook is called and RSA public key is loaded from configuration

### Testing the API
After starting the application, test the endpoint:
```bash
curl https://localhost:7161/weatherforecast
```
Check the appropriate log files to see the logged messages.

## Decrypting Log Files

To decrypt the production logs, you can use the CLI tool:
```bash
# The log file name will include the date (e.g., prod20251129.txt)
serilog-encrypt decrypt -i logs/prod20251129.txt -o logs/prod_decrypted.txt -k private_key.xml
```

**Note**: You will need the private key file (`private_key.xml`) that was generated earlier with the `serilog-encrypt generate` command.

## Troubleshooting

### Common Issues

1. **"Could not find a public static property or field with name `SetupHook`"**
   - Ensure the `EncryptSetup` class is in the correct namespace
   - Verify the hooks parameter syntax in `appsettings.Production.json`
   - Make sure the assembly name matches your project name

2. **Encryption hook running in Development**
   - Check that `ASPNETCORE_ENVIRONMENT` is set to "Development"
   - Verify that `appsettings.Development.json` does not contain a `hooks` parameter
   - Check launch profiles in `Properties/launchSettings.json`

3. **"LogPublicKey not found in configuration"**
   - Ensure the `LogPublicKey` is present in `appsettings.Production.json`
   - Verify that the key generation step was completed successfully

## Key Security Considerations

- **Never commit private keys to source control** the one in this project is for demonstration only
- Store the `LogPublicKey` in environment variables or secure key management in production
- Consider rotating keys periodically
- Ensure proper access controls on encrypted log files

## Conclusion
This example demonstrates how to set up Serilog logging in an ASP.NET Core Web API project with environment-specific configurations, including encrypted logging for production. Adjust the configurations and logging sinks as needed for your specific requirements.
## Note
This example is for demonstration purposes only. In a real-world application, ensure that you handle sensitive information, such as encryption keys, securely and follow best practices for application security.
