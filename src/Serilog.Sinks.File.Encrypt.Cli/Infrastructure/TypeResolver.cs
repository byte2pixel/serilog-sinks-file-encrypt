using Spectre.Console.Cli;

namespace Serilog.Sinks.File.Encrypt.Cli.Infrastructure;

/// <summary>
/// Resolves types using an IServiceProvider.
/// </summary>
/// <param name="provider"></param>
public sealed class TypeResolver(IServiceProvider provider) : ITypeResolver, IDisposable
{
    private readonly IServiceProvider _provider =
        provider ?? throw new ArgumentNullException(nameof(provider));

    /// <summary>
    /// Resolves a type using the underlying service provider.
    /// </summary>
    /// <param name="type">The type to resolve.</param>
    /// <returns></returns>
    public object? Resolve(Type? type)
    {
        return type == null ? null : _provider.GetService(type);
    }

    /// <summary>
    /// Disposes the underlying service provider if it implements IDisposable.
    /// </summary>
    public void Dispose()
    {
        if (_provider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
