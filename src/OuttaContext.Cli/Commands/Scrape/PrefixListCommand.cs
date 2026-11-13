using System.ComponentModel;
using OuttaContext.Cli.Console;
using OuttaContext.Core.Common;
using OuttaContext.Core.Scraping;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Scrape;

[Description("Show built-in link rules and registered prefixes.")]
public sealed class PrefixListCommand(IPrefixStore prefixes, ScraperRegistry scrapers, SpectreReporter reporter, IAnsiConsole console)
    : Command<PrefixListSettings>
{
    public override int Execute(CommandContext context, PrefixListSettings settings, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<Scraper>, Error> shown = settings.Site is { } site
            ? scrapers.Find(site).Map(scraper => (IReadOnlyList<Scraper>)[scraper])
            : Result.Success<IReadOnlyList<Scraper>, Error>(scrapers.Scrapers);
        return shown.ToExitCode(reporter, list =>
        {
            foreach (Scraper scraper in list)
            {
                Print(scraper);
            }
        });
    }

    private void Print(Scraper scraper)
    {
        console.MarkupLine($"[bold]{Markup.Escape(scraper.Label)}[/]");
        foreach (LinkRule rule in scraper.LinkRules)
        {
            console.MarkupLine($"  [dim]built-in[/]  {Markup.Escape(rule.Kind)}");
        }

        IReadOnlyList<LinkPrefix> extra = prefixes.ForSite(scraper.Site);
        foreach (LinkPrefix prefix in extra)
        {
            console.MarkupLine($"  [green]prefix[/]    {Markup.Escape(prefix.Value)}");
        }

        if (extra.Count == 0)
        {
            console.MarkupLine("  [dim](no registered prefixes)[/]");
        }
    }
}
