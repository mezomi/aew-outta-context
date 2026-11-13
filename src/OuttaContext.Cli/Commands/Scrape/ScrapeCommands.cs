using System.ComponentModel;
using System.Text.Json;
using OuttaContext.Cli.Console;
using OuttaContext.Core.Scraping;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Scrape;

public sealed class ScrapeSiteSettings : GlobalSettings
{
    [CommandArgument(0, "<PAGE_URL>")]
    [Description("The web page to search.")]
    public string PageUrl { get; init; } = string.Empty;

    [CommandOption("--json")]
    [Description("Print results as JSON.")]
    public bool Json { get; init; }
}

/// <summary>`outta scrape SITE PAGE_URL`. One instance is registered per scraper; the site comes from the command's data.</summary>
[Description("Find video links on a web page (nothing is downloaded).")]
public sealed class ScrapeSiteCommand(ScrapeHandler handler, SpectreReporter reporter, IAnsiConsole console) : AsyncCommand<ScrapeSiteSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ScrapeSiteSettings settings, CancellationToken cancellationToken)
    {
        string site = (string)context.Data!;
        if (settings.Json)
        {
            reporter.Verbosity = Core.Reporting.Verbosity.Quiet;
        }

        IReadOnlyList<FoundVideo> found = await handler.HandleAsync(site, settings.PageUrl, cancellationToken);
        if (settings.Json)
        {
            var rows = found.Select(video => new { url = video.Url, id = video.VideoId, kind = video.Kind, page_url = video.PageUrl });
            console.WriteLine(JsonSerializer.Serialize(rows, JsonOutput.Indented));
            return 0;
        }

        reporter.Success($"Found {found.Count} video{(found.Count == 1 ? string.Empty : "s")}:");
        int width = found.Max(video => video.Url.Length);
        for (int index = 0; index < found.Count; index++)
        {
            console.MarkupLine($"  {index + 1}. {Markup.Escape(found[index].Url.PadRight(width))}  [dim]({Markup.Escape(found[index].Kind)})[/]");
        }

        return 0;
    }
}

public sealed class PrefixSettings : GlobalSettings
{
    [CommandArgument(0, "<SITE>")]
    [Description("Site, e.g. dailymotion.")]
    public string Site { get; init; } = string.Empty;

    [CommandArgument(1, "<PREFIX>")]
    [Description("Start of matching links, e.g. \"https://geo.dailymotion.com/player/\".")]
    public string Prefix { get; init; } = string.Empty;
}

public sealed class PrefixListSettings : GlobalSettings
{
    [CommandArgument(0, "[SITE]")]
    [Description("Only this site.")]
    public string? Site { get; init; }
}

[Description("Register a link prefix for a site.")]
public sealed class PrefixAddCommand(IPrefixStore prefixes, ScraperRegistry scrapers, SpectreReporter reporter) : Command<PrefixSettings>
{
    public override int Execute(CommandContext context, PrefixSettings settings, CancellationToken cancellationToken)
    {
        string site = scrapers.Get(settings.Site).Site;
        prefixes.Add(site, settings.Prefix);
        reporter.Success($"Added {settings.Prefix} for {site}");
        return 0;
    }
}

[Description("Remove a registered link prefix.")]
public sealed class PrefixRemoveCommand(IPrefixStore prefixes, ScraperRegistry scrapers, SpectreReporter reporter) : Command<PrefixSettings>
{
    public override int Execute(CommandContext context, PrefixSettings settings, CancellationToken cancellationToken)
    {
        string site = scrapers.Get(settings.Site).Site;
        prefixes.Remove(site, settings.Prefix);
        reporter.Success($"Removed {settings.Prefix} from {site}");
        return 0;
    }
}

[Description("Show built-in link rules and registered prefixes.")]
public sealed class PrefixListCommand(IPrefixStore prefixes, ScraperRegistry scrapers, IAnsiConsole console) : Command<PrefixListSettings>
{
    public override int Execute(CommandContext context, PrefixListSettings settings, CancellationToken cancellationToken)
    {
        IEnumerable<Scraper> shown = settings.Site is { } site ? [scrapers.Get(site)] : scrapers.Scrapers;
        foreach (Scraper scraper in shown)
        {
            console.MarkupLine($"[bold]{Markup.Escape(scraper.Label)}[/]");
            foreach (LinkRule rule in scraper.LinkRules)
            {
                console.MarkupLine($"  [dim]built-in[/]  {Markup.Escape(rule.Kind)}");
            }

            IReadOnlyList<string> extra = prefixes.ForSite(scraper.Site);
            foreach (string prefix in extra)
            {
                console.MarkupLine($"  [green]prefix[/]    {Markup.Escape(prefix)}");
            }

            if (extra.Count == 0)
            {
                console.MarkupLine("  [dim](no registered prefixes)[/]");
            }
        }

        return 0;
    }
}
