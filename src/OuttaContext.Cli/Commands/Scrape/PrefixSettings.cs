using System.ComponentModel;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Scrape;

/// <summary>Options of `outta scrape prefixes add|remove SITE PREFIX`.</summary>
public sealed class PrefixSettings : GlobalSettings
{
    [CommandArgument(0, "<SITE>")]
    [Description("Site, e.g. dailymotion.")]
    public string Site { get; init; } = string.Empty;

    [CommandArgument(1, "<PREFIX>")]
    [Description("Start of matching links, e.g. \"https://geo.dailymotion.com/player/\".")]
    public string Prefix { get; init; } = string.Empty;
}
