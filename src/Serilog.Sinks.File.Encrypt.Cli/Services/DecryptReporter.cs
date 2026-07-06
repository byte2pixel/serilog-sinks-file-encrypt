using System.Text.Json;
using Spectre.Console;

namespace Serilog.Sinks.File.Encrypt.Cli;

/// <inheritdoc />
public sealed class DecryptReporter(IConsoleWriter writer) : IDecryptReporter
{
    /// <inheritdoc />
    public void ReportSessions(FileReport report)
    {
        if (report.Sessions.Count == 0)
        {
            return;
        }

        Table table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Session")
            .AddColumn("Version")
            .AddColumn("KeyId")
            .AddColumn("Messages")
            .AddColumn("Failed")
            .AddColumn("Seal");

        foreach (SessionReport session in report.Sessions)
        {
            table.AddRow(
                session.Index.ToString(),
                $"v{session.FormatVersion}",
                Markup.Escape(session.KeyId),
                session.DecryptedMessages.ToString(),
                session.FailedMessages.ToString(),
                FormatSeal(session)
            );
        }

        writer.Info(table);
    }

    /// <inheritdoc />
    public void ReportSummary(RunSummary summary)
    {
        Table table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]Summary[/]")
            .AddColumn("Files")
            .AddColumn("Succeeded")
            .AddColumn("Failed")
            .AddColumn("Refused")
            .AddColumn("Nothing decrypted")
            .AddColumn("Messages")
            .AddColumn("Resyncs");

        table.AddRow(
            summary.Files.ToString(),
            $"[green]{summary.Succeeded}[/]",
            summary.Failed > 0 ? $"[red]{summary.Failed}[/]" : "0",
            summary.Refused > 0 ? $"[yellow]{summary.Refused}[/]" : "0",
            summary.NothingDecrypted > 0 ? $"[yellow]{summary.NothingDecrypted}[/]" : "0",
            summary.TotalMessages.ToString(),
            summary.TotalResyncAttempts.ToString()
        );

        writer.Info(table);

        if (summary.Failed > 0)
        {
            writer.Warning($"  [red]✗ Failed:[/] {summary.Failed}");
        }
        if (summary.Refused > 0)
        {
            writer.Warning($"  [yellow]⚠ Refused:[/] {summary.Refused}");
        }
        if (summary.NothingDecrypted > 0)
        {
            writer.Warning($"  [yellow]⚠ Nothing decrypted:[/] {summary.NothingDecrypted}");
        }
    }

    /// <inheritdoc />
    public string ToJson(DecryptRunReport report)
    {
        return JsonSerializer.Serialize(report, DecryptReportJsonContext.Default.DecryptRunReport);
    }

    private static string FormatSeal(SessionReport session) =>
        session.SealStatus switch
        {
            nameof(Serilog.Sinks.File.Decrypt.Models.SealStatus.Sealed) => "[green]Sealed[/]",
            nameof(Serilog.Sinks.File.Decrypt.Models.SealStatus.NotApplicable) =>
                "[dim]v1 (n/a)[/]",
            nameof(Serilog.Sinks.File.Decrypt.Models.SealStatus.Unsealed) => "[yellow]Unsealed[/]",
            nameof(Serilog.Sinks.File.Decrypt.Models.SealStatus.SealCountMismatch) =>
                $"[red]Count mismatch (declared {session.DeclaredFrameCount})[/]",
            nameof(Serilog.Sinks.File.Decrypt.Models.SealStatus.SealInvalid) => "[red]Invalid[/]",
            _ => Markup.Escape(session.SealStatus),
        };
}
