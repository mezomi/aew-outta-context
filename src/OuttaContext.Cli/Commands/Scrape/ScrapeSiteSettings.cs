using System.ComponentModel;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Scrape;

/// <summary>Options of `outta scrape SITE`.</summary>
public sealed class ScrapeSiteSettings : GlobalSettings
{
    [CommandArgument(0, "<PAGE_URL>")]
    [Description("The web page to search.")]
    public string PageUrl { get; init; } = string.Empty;

    [CommandOption("--json")]
    [Description("Print results as JSON.")]
    public bool Json { get; init; }
}
