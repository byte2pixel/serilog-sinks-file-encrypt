namespace Serilog.Sinks.File.Encrypt.Models;

/// <summary>
/// Defines the format for the RSA keys.
/// </summary>
public enum KeyFormat
{
    /// <summary>
    /// Base64 XML
    ///
    /// <RSAKeyValue>
    ///     <Modulus>
    ///        ...
    ///    </Modulus>
    ///    <Exponent>
    ///        ...
    ///    </Exponent>
    /// </RSAKeyValue>
    /// </summary>
    Xml,
    /// <summary>
    /// Base64 PEM (RFC 7468)
    ///
    /// -----BEGIN RSA PUBLIC KEY-----
    ///     ....
    /// -----END RSA PUBLIC KEY-----
    /// </summary>
    Pem
}
