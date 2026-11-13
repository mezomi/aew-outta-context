using Microsoft.Extensions.DependencyInjection;
using OuttaContext.Cli.Commands;
using OuttaContext.Cli.Commands.Clips;
using OuttaContext.Cli.Commands.Config;
using OuttaContext.Cli.Commands.Doctor;
using OuttaContext.Cli.Commands.Episode;
using OuttaContext.Cli.Commands.Scrape;
using OuttaContext.Cli.Commands.Storage;
using OuttaContext.Cli.Commands.Update;
using OuttaContext.Cli.Commands.Upload;
using OuttaContext.Cli.Console;
using OuttaContext.Core.Scraping;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Composition;

/// <summary>The `outta` command tree. The folder layout under Commands/ mirrors it.</summary>
public static class OuttaApp
{
    public static CommandApp Create(IServiceCollection services)
    {
        var app = new CommandApp(new TypeRegistrar(services));
        app.Configure(config => Configure(config, services));
        return app;
    }

    public static void Configure(IConfigurator config, IServiceCollection services)
    {
        // Both were registered as ready-made instances, so these are the same objects commands receive.
        IAnsiConsole console = Instance<IAnsiConsole>(services);
        SpectreReporter reporter = Instance<SpectreReporter>(services);

        config.SetApplicationName("outta");
        config.SetApplicationVersion(typeof(OuttaApp).Assembly.GetName().Version?.ToString(3) ?? "0.0.0");
        config.ConfigureConsole(console);
        config.SetInterceptor(new VerbosityInterceptor(reporter));
        config.SetExceptionHandler((exception, _) => ErrorHandler.Handle(exception, console, reporter));
        config.CancellationExitCode(ErrorHandler.Cancelled);

        config.AddCommand<ClipCommand>("clip")
            .WithExample("clip", "\"https://youtu.be/dQw4w9WgXcQ\"", "--range", "0:45-1:05", "--title", "\"Table spot\"");

        config.AddBranch("scrape", scrape =>
        {
            scrape.SetDescription("Find video links on a web page.");
            foreach (Scraper scraper in new ScraperRegistry().Scrapers)
            {
                scrape.AddCommand<ScrapeSiteCommand>(scraper.Site)
                    .WithData(scraper.Site)
                    .WithDescription($"Find {scraper.Label} video links on a web page (nothing is downloaded).");
            }

            scrape.AddBranch("prefixes", prefixes =>
            {
                prefixes.SetDescription("Extra link prefixes the scrapers treat as videos.");
                prefixes.AddCommand<PrefixAddCommand>("add");
                prefixes.AddCommand<PrefixListCommand>("list");
                prefixes.AddCommand<PrefixRemoveCommand>("remove");
            });
        });

        config.AddBranch("episode", episode =>
        {
            episode.SetDescription("Group clips into ~10-minute episodes.");
            episode.AddCommand<EpisodeStatusCommand>("status");
            episode.AddCommand<EpisodeListCommand>("list");
            episode.AddCommand<EpisodeNewCommand>("new");
            episode.AddCommand<EpisodeMoveCommand>("move");
            episode.AddCommand<EpisodeRemoveCommand>("remove");
            episode.AddCommand<EpisodeBuildCommand>("build");
        });

        config.AddBranch("storage", storage =>
        {
            storage.SetDescription("Where clips and episodes are uploaded.");
            storage.AddCommand<StorageAddCommand>("add");
            storage.AddCommand<StorageListCommand>("list");
            storage.AddCommand<StorageTestCommand>("test");
            storage.AddCommand<StorageDefaultCommand>("default");
            storage.AddCommand<StorageRemoveCommand>("remove");
        });

        config.AddCommand<UploadCommand>("upload");

        config.AddBranch("config", settings =>
        {
            settings.SetDescription("Clip folder, episode length, clip cap and quality.");
            settings.AddCommand<SetDestinationCommand>("set-destination");
            settings.AddCommand<ConfigSetCommand>("set");
            settings.AddCommand<ConfigShowCommand>("show");
        });

        config.AddCommand<DoctorCommand>("doctor");
        config.AddCommand<UpdateCommand>("update");
    }

    private static T Instance<T>(IServiceCollection services) =>
        (T)services.Last(descriptor => descriptor.ServiceType == typeof(T)).ImplementationInstance!;
}
