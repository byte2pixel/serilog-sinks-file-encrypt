using Spectre.Console;

namespace Serilog.Sinks.File.Encrypt.Cli;

/// <summary>
/// Default <see cref="IConsoleWriter"/> writing Spectre.Console markup to the injected
/// console.
/// </summary>
/// <param name="console">The ANSI console to write to.</param>
public sealed class ConsoleWriter(IAnsiConsole console) : IConsoleWriter
{
    /// <inheritdoc />
    public Verbosity Verbosity { get; set; } = Verbosity.Normal;

    /// <inheritdoc />
    public void Info(FormattableString markup)
    {
        if (Verbosity != Verbosity.Quiet)
        {
            console.MarkupLineInterpolated(markup);
        }
    }

    /// <inheritdoc />
    public void Verbose(FormattableString markup)
    {
        if (Verbosity == Verbosity.Verbose)
        {
            console.MarkupLineInterpolated(markup);
        }
    }

    /// <inheritdoc />
    public void Warning(FormattableString markup)
    {
        console.MarkupLineInterpolated(markup);
    }

    /// <inheritdoc />
    public void Error(FormattableString markup)
    {
        console.MarkupLineInterpolated(markup);
    }

    /// <inheritdoc />
    public void BlankLine()
    {
        if (Verbosity != Verbosity.Quiet)
        {
            console.WriteLine();
        }
    }
}
