using System.ComponentModel;
using System.Text.Json;
using OuttaContext.Cli.Console;
using OuttaContext.Core.Reporting;
using OuttaContext.Core.Scraping;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Scrape;

/// <summary>`outta scrape SITE PAGE_URL`. One is registered per scraper; the site comes from the command's data.</summary>
[Description("Find video links on a web page (nothing is downloaded).")]
public sealed class ScrapeSiteCommand(ScrapeHandler handler, SpectreReporter reporter, IAnsiConsole console) : AsyncCommand<ScrapeSiteSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, ScrapeSiteSettings settings, CancellationToken cancellationToken)
    {
        string site = (string)context.Data!;
        if (settings.Json)
        {
            reporter.Verbosity = Verbosity.Quiet;
        }

        return handler.HandleAsync(site, settings.PageUrl, cancellationToken)
            .ToExitCode(reporter, found => Print(found, settings.Json));
    }

    private void Print(IReadOnlyList<FoundVideo> found, bool json)
    {
        if (json)
        {
            var rows = found.Select(video => new { url = video.Url, id = video.VideoId, kind = video.Kind, page_url = video.PageUrl });
            console.WriteLine(JsonSerializer.Serialize(rows, JsonOutput.Indented));
            return;
        }

        reporter.Success($"Found {found.Count} video{(found.Count == 1 ? string.Empty : "s")}:");
        int width = found.Max(video => video.Url.Length);
        for (int index = 0; index < found.Count; index++)
        {
            console.MarkupLine($"  {index + 1}. {Markup.Escape(found[index].Url.PadRight(width))}  [dim]({Markup.Escape(found[index].Kind)})[/]");
        }
    }
}
