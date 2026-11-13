using System.ComponentModel;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Scrape;

/// <summary>Options of `outta scrape prefixes list [SITE]`.</summary>
public sealed class PrefixListSettings : GlobalSettings
{
    [CommandArgument(0, "[SITE]")]
    [Description("Only this site.")]
    public string? Site { get; init; }
}
