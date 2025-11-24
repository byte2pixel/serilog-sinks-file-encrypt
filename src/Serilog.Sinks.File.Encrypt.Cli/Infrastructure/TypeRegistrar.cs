using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace Serilog.Sinks.File.Encrypt.Cli.Infrastructure;

/// <summary>
/// The type registrar for Spectre.Console.Cli using Microsoft.Extensions.DependencyInjection.
/// </summary>
/// <param name="builder">The service collection builder.</param>
public class TypeRegistrar(IServiceCollection builder) : ITypeRegistrar
{
    private readonly IServiceCollection _builder =
        builder ?? throw new ArgumentNullException(nameof(builder));

    /// <summary>
    /// Builds the type resolver.
    /// </summary>
    /// <returns></returns>
    public ITypeResolver Build()
    {
        return new TypeResolver(_builder.BuildServiceProvider());
    }

    /// <summary>
    /// Registers a service with its implementation.
    /// </summary>
    /// <param name="service">The service type.</param>
    /// <param name="implementation">The implementation type.</param>
    public void Register(Type service, Type implementation)
    {
        _builder.AddSingleton(service, implementation);
    }

    /// <summary>
    /// Registers a service with a specific instance.
    /// </summary>
    /// <param name="service">The service type.</param>
    /// <param name="implementation">The implementation type.</param>
    public void RegisterInstance(Type service, object implementation)
    {
        _builder.AddSingleton(service, implementation);
    }

    /// <summary>
    /// Registers a service with a factory method for lazy initialization.
    /// </summary>
    /// <param name="service">The service type.</param>
    /// <param name="factory">The factory method to create the service instance.</param>
    public void RegisterLazy(Type service, Func<object> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _builder.AddSingleton(service, _ => factory());
    }
}
