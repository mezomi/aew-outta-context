using System.ComponentModel;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands;

/// <summary>Options every command accepts. All command settings inherit from this.</summary>
public class GlobalSettings : CommandSettings
{
    [CommandOption("-v|--verbose")]
    [Description("Show details: yt-dlp/ffmpeg output and commands.")]
    public bool Verbose { get; init; }

    [CommandOption("-q|--quiet")]
    [Description("Only show results and errors.")]
    public bool Quiet { get; init; }

    public override Spectre.Console.ValidationResult Validate() =>
        Verbose && Quiet
            ? Spectre.Console.ValidationResult.Error("Use either --verbose or --quiet, not both.")
            : base.Validate();
}
