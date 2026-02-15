namespace Serilog.Sinks.File.Encrypt.Models;

/// <summary>
/// Factory for creating <see cref="HeaderMetadata"/> instances based on the specified version.
/// </summary>
public static class HeaderMetadataFactory
{
    /// <summary>
    /// Creates a <see cref="HeaderMetadata"/> instance based on the specified version.
    /// </summary>
    /// <param name="version">The byte that represents the version.</param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException">When receiving a version that isn't supported.</exception>
    public static HeaderMetadata Create(byte version)
    {
        return version switch
        {
            1 => HeaderMetadata.CreateV1(),
            _ => throw new NotSupportedException($"Unsupported header version: {version}"),
        };
    }
}
