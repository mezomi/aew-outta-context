using System.ComponentModel;
using OuttaContext.Core.Doctor;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Doctor;

public sealed class DoctorSettings : GlobalSettings
{
    [CommandOption("--no-storage")]
    [Description("Skip testing storage logins.")]
    public bool NoStorage { get; init; }
}

[Description("Check ffmpeg, yt-dlp, rclone and storage logins, and print fixes.")]
public sealed class DoctorCommand(DoctorService doctor, IAnsiConsole console) : AsyncCommand<DoctorSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, DoctorSettings settings, CancellationToken cancellationToken)
    {
        IReadOnlyList<DoctorCheck> checks = await doctor.RunAsync(!settings.NoStorage, cancellationToken);
        int width = checks.Max(check => check.Name.Length);
        foreach (DoctorCheck check in checks)
        {
            string icon = check.State switch
            {
                CheckState.Ok => "[green]✓[/]",
                CheckState.Warn => "[yellow]![/]",
                _ => "[red]✗[/]",
            };
            console.MarkupLine($"{icon} {check.Name.PadRight(width)}  {Markup.Escape(check.Message)}");
            if (check.State != CheckState.Ok && check.Fix.Length > 0)
            {
                console.MarkupLine($"  {new string(' ', width)}  [dim]fix:[/] {Markup.Escape(check.Fix)}");
            }
        }

        int failed = checks.Count(check => check.State == CheckState.Fail);
        console.MarkupLine(failed > 0 ? $"\n[red]{failed} problem(s) found.[/]" : "\n[green]All required tools are ready.[/]");
        return failed > 0 ? 1 : 0;
    }
}
