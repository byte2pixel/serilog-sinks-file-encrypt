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

// csharpier-ignore
string[] summaries = ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

app.MapGet(
        "/weatherforecast",
        ([FromServices] ILogger<Program> logger) =>
        {
            var stopwatch = Stopwatch.StartNew();
            logger.LogDebug("Starting weather forecast generation");
            logger.LogInformation("Generating weather forecast");
            WeatherForecast[] forecast = Enumerable
                .Range(1, 5)
                .Select(index => new WeatherForecast(
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    Random.Shared.Next(-20, 55),
                    summaries[Random.Shared.Next(summaries.Length)]
                ))
                .ToArray();
            stopwatch.Stop();
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "Completed weather forecast generation in {ElapsedMilliseconds} ms",
                    stopwatch.ElapsedMilliseconds
                );
            }
            return forecast;
        }
    )
    .WithName("GetWeatherForecast");

await app.RunAsync();
