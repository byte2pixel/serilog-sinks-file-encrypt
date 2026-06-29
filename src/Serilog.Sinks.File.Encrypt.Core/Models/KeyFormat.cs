namespace Serilog.Sinks.File.Encrypt.Models;

/// <summary>
/// Defines the format for the RSA keys.
/// </summary>
public enum KeyFormat
{
    /// <summary>
    /// Base64 XML
    /// <code>
    /// &lt;RSAKeyValue&gt;
    ///     &lt;Modulus&gt;
    ///        ...
    ///    &lt;/Modulus&gt;
    ///    &lt;Exponent&gt;
    ///        ...
    ///    &lt;/Exponent&gt;
    /// &lt;/RSAKeyValue&gt;
    /// </code>
    /// </summary>
    Xml,

    /// <summary>
    /// Base64 PEM (RFC 7468)
    /// <code>
    /// -----BEGIN RSA PUBLIC KEY-----
    /// ....
    /// -----END RSA PUBLIC KEY-----
    /// </code>
    /// </summary>
    Pem,
}
