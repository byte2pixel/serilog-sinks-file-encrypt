using System.IO.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace Serilog.Sinks.File.Encrypt.Cli.Infrastructure;

/// <summary>
/// Contains extension methods for IServiceCollection
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the CLI services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The updated service collection</returns>
    public static IServiceCollection AddCliServices(this IServiceCollection services)
    {
        services.AddTransient<IFileSystem>(_ => new FileSystem());
        services.AddSingleton(AnsiConsole.Console);
        return services;
    }
}
