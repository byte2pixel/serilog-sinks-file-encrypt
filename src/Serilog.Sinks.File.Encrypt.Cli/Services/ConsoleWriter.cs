using Spectre.Console;
using Spectre.Console.Rendering;

namespace Serilog.Sinks.File.Encrypt.Cli;

/// <summary>
/// Default <see cref="IConsoleWriter"/> writing Spectre.Console markup to the injected
/// console, with an optional error channel for --json runs.
/// </summary>
public sealed class ConsoleWriter : IConsoleWriter
{
    private readonly IAnsiConsole _stdout;
    private IAnsiConsole _target;

    /// <summary>
    /// Creates a writer over the console bound to standard output.
    /// </summary>
    /// <param name="console">The ANSI console bound to standard output.</param>
    public ConsoleWriter(IAnsiConsole console)
    {
        _stdout = console;
        _target = console;
    }

    /// <summary>
    /// Creates the console used by <see cref="UseErrorChannel"/>. Defaults to a console
    /// over stderr; tests replace it with a capturing console.
    /// </summary>
    public Func<IAnsiConsole> ErrorConsoleFactory { get; set; } =
        () =>
            AnsiConsole.Create(
                new AnsiConsoleSettings
                {
                    Ansi = AnsiSupport.Detect,
                    Interactive = InteractionSupport.No,
                    Out = new AnsiConsoleOutput(Console.Error),
                }
            );

    /// <inheritdoc />
    public Verbosity Verbosity { get; set; } = Verbosity.Normal;

    /// <inheritdoc />
    public void Info(FormattableString markup)
    {
        if (Verbosity != Verbosity.Quiet)
        {
            _target.MarkupLineInterpolated(markup);
        }
    }

    /// <inheritdoc />
    public void Verbose(FormattableString markup)
    {
        if (Verbosity == Verbosity.Verbose)
        {
            _target.MarkupLineInterpolated(markup);
        }
    }

    /// <inheritdoc />
    public void Warning(FormattableString markup)
    {
        _target.MarkupLineInterpolated(markup);
    }

    /// <inheritdoc />
    public void Error(FormattableString markup)
    {
        _target.MarkupLineInterpolated(markup);
    }

    /// <inheritdoc />
    public void BlankLine()
    {
        if (Verbosity != Verbosity.Quiet)
        {
            _target.WriteLine();
        }
    }

    /// <inheritdoc />
    public void Info(IRenderable renderable)
    {
        if (Verbosity != Verbosity.Quiet)
        {
            _target.Write(renderable);
        }
    }

    /// <inheritdoc />
    public void UseErrorChannel()
    {
        _target = ErrorConsoleFactory();
    }

    /// <inheritdoc />
    public void Raw(string text)
    {
        // Straight to the stdout writer: bypasses markup parsing and width-based wrapping,
        // which would otherwise be able to split JSON string literals.
        _stdout.Profile.Out.Writer.Write(text);
        _stdout.Profile.Out.Writer.Flush();
    }
}
