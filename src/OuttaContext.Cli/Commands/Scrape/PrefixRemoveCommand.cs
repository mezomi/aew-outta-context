using System.ComponentModel;
using OuttaContext.Cli.Console;
using OuttaContext.Core.Scraping;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Scrape;

[Description("Remove a registered link prefix.")]
public sealed class PrefixRemoveCommand(IPrefixStore prefixes, ScraperRegistry scrapers, SpectreReporter reporter) : Command<PrefixSettings>
{
    public override int Execute(CommandContext context, PrefixSettings settings, CancellationToken cancellationToken) =>
        scrapers.Find(settings.Site)
            .Map(scraper => (scraper.Site, Prefix: LinkPrefix.FromStored(settings.Prefix)))
            .Check(entry => prefixes.Remove(entry.Site, entry.Prefix))
            .ToExitCode(reporter, entry => reporter.Success($"Removed {entry.Prefix} from {entry.Site}"));
}
