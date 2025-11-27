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
    /// <param name="fileSystem">Optional file system implementation. If null, uses the real file system.</param>
    /// <returns>The updated service collection</returns>
    public static IServiceCollection AddCliServices(
        this IServiceCollection services,
        IFileSystem? fileSystem = null
    )
    {
        services.AddTransient<IFileSystem>(_ => fileSystem ?? new FileSystem());
        services.AddSingleton(AnsiConsole.Console);
        return services;
    }
}
