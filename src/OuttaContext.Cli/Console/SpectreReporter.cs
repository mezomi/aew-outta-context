using OuttaContext.Core.Reporting;
using Spectre.Console;

namespace OuttaContext.Cli.Console;

/// <summary>
/// <see cref="IReporter"/> drawn with Spectre.Console: arrows for steps, check marks for results,
/// live progress bars. The only place that knows how output looks; use cases never touch Spectre.
/// </summary>
public sealed class SpectreReporter(IAnsiConsole console) : IReporter
{
    public Verbosity Verbosity { get; set; } = Verbosity.Normal;

    public void Step(string message)
    {
        if (Verbosity >= Verbosity.Normal)
        {
            console.MarkupLine($"[cyan]→[/] {Markup.Escape(message)}");
        }
    }

    public void Success(string message) => console.MarkupLine($"[green]✓[/] {Markup.Escape(message)}");

    public void Warn(string message) => console.MarkupLine($"[yellow]![/] {Markup.Escape(message)}");

    public void Detail(string message)
    {
        if (Verbosity >= Verbosity.Verbose)
        {
            console.MarkupLine($"  [dim]{Markup.Escape(message)}[/]");
        }
    }

    public void Info(string message) => console.WriteLine(message);

    /// <summary>Prints the one standard error format: <c>error: …</c> then <c>hint: …</c>.</summary>
    public void Error(string message, string hint)
    {
        console.MarkupLine($"[bold red]error:[/] {Markup.Escape(message)}");
        if (hint.Length > 0)
        {
            console.MarkupLine($"[yellow]hint:[/] {Markup.Escape(hint)}");
        }
    }

    public IProgressTask StartProgress(string description, ProgressUnit unit, double? total = null) =>
        Verbosity < Verbosity.Normal || !console.Profile.Capabilities.Interactive
            ? new QuietProgressTask()
            : new LiveProgressTask(console, description, unit, total);
}
