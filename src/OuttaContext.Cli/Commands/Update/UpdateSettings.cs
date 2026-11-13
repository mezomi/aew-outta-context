using System.ComponentModel;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Update;

/// <summary>Options of `outta update`.</summary>
public sealed class UpdateSettings : GlobalSettings
{
    [CommandOption("--check")]
    [Description("Only say whether an update is available.")]
    public bool Check { get; init; }

    [CommandOption("--yt-dlp-only")]
    [Description("Only install or update yt-dlp; leave outta.exe as it is.")]
    public bool YtDlpOnly { get; init; }
}
