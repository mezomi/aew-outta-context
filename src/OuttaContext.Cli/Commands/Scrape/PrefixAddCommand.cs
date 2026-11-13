using System.ComponentModel;
using OuttaContext.Cli.Console;
using OuttaContext.Core.Scraping;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Scrape;

[Description("Register a link prefix for a site.")]
public sealed class PrefixAddCommand(IPrefixStore prefixes, ScraperRegistry scrapers, SpectreReporter reporter) : Command<PrefixSettings>
{
    public override int Execute(CommandContext context, PrefixSettings settings, CancellationToken cancellationToken) =>
        scrapers.Find(settings.Site)
            .Bind(scraper => LinkPrefix.Create(settings.Prefix).Map(prefix => (scraper.Site, prefix)))
            .Check(entry => prefixes.Add(entry.Site, entry.prefix))
            .ToExitCode(reporter, entry => reporter.Success($"Added {entry.prefix} for {entry.Site}"));
}
