using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Serilog.Sinks.File.Encrypt.Cli.Commands;

/// <summary>
/// Settings shared by every serilog-encrypt command: output verbosity control.
/// </summary>
public class GlobalCommandSettings : CommandSettings
{
    /// <summary>
    /// Suppress informational output; warnings and errors are still written.
    /// </summary>
    [CommandOption("-q|--quiet")]
    [Description("Suppress informational output (warnings and errors are still shown)")]
    [DefaultValue(false)]
    public bool Quiet { get; init; }

    /// <summary>
    /// Write additional diagnostic detail.
    /// </summary>
    [CommandOption("-v|--verbose")]
    [Description("Show additional diagnostic detail")]
    [DefaultValue(false)]
    public bool Verbose { get; init; }

    /// <summary>
    /// The effective verbosity derived from <see cref="Quiet"/> and <see cref="Verbose"/>.
    /// </summary>
    public Verbosity Verbosity =>
        Quiet ? Verbosity.Quiet
        : Verbose ? Verbosity.Verbose
        : Verbosity.Normal;

    /// <inheritdoc />
    public override ValidationResult Validate()
    {
        if (Quiet && Verbose)
        {
            return ValidationResult.Error("✗ Error: --quiet and --verbose cannot be combined.");
        }

        return base.Validate();
    }
}
