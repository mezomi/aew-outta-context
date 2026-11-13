using Microsoft.Extensions.DependencyInjection;
using OuttaContext.Cli.Console;
using OuttaContext.Core.Clips;
using OuttaContext.Core.Doctor;
using OuttaContext.Core.Episodes;
using OuttaContext.Core.Library;
using OuttaContext.Core.Media;
using OuttaContext.Core.Paths;
using OuttaContext.Core.Reporting;
using OuttaContext.Core.Scraping;
using OuttaContext.Core.Settings;
using OuttaContext.Core.Sources;
using OuttaContext.Core.Storage;
using OuttaContext.Core.Uploads;
using OuttaContext.Infrastructure.Credentials;
using OuttaContext.Infrastructure.FFmpeg;
using OuttaContext.Infrastructure.Media;
using OuttaContext.Infrastructure.Persistence;
using OuttaContext.Infrastructure.Processes;
using OuttaContext.Infrastructure.Storage;
using OuttaContext.Infrastructure.Updates;
using OuttaContext.Infrastructure.Web;
using OuttaContext.Infrastructure.YtDlp;
using Spectre.Console;

namespace OuttaContext.Cli.Composition;

/// <summary>
/// Wires every interface to its real implementation. Everything is created lazily, so `--help`
/// doesn't touch the database or the network. Tests replace pieces with fakes afterwards.
/// </summary>
public static class OuttaServices
{
    public static IServiceCollection Create(IAnsiConsole console, AppPaths paths)
    {
        var services = new ServiceCollection();

        // Console and settings
        services.AddSingleton(console);
        services.AddSingleton(paths);
        // One reporter instance, shared by the error handler, the --verbose interceptor and every command.
        services.AddSingleton(new SpectreReporter(console));
        services.AddSingleton<IReporter>(sp => sp.GetRequiredService<SpectreReporter>());
        services.AddSingleton<ISettingsStore>(_ => new TomlSettingsStore(paths.SettingsFile));
        services.AddSingleton(sp => sp.GetRequiredService<ISettingsStore>().Load());
        services.AddSingleton(TimeProvider.System);

        // Outside programs
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton(_ => new ToolLocator(paths));
        services.AddSingleton(sp => new YtDlpOptions
        {
            CookiesFromBrowser = sp.GetRequiredService<OuttaSettings>().CookiesFromBrowser,
            Debug = () => sp.GetRequiredService<SpectreReporter>().Verbosity == Verbosity.Verbose,
        });
        services.AddSingleton<IVideoResolver, YtDlpVideoResolver>();
        services.AddSingleton<IStreamFetcher>(sp => new YtDlpStreamFetcher(
            sp.GetRequiredService<IProcessRunner>(), sp.GetRequiredService<ToolLocator>(),
            sp.GetRequiredService<YtDlpOptions>(), sp.GetRequiredService<IReporter>().Detail));
        services.AddSingleton<IMediaProcessor>(sp => new FfmpegMediaProcessor(
            sp.GetRequiredService<IProcessRunner>(), sp.GetRequiredService<ToolLocator>(), sp.GetRequiredService<IReporter>().Detail));
        services.AddSingleton<IClipDownloader, ClipDownloader>();

        // Library and episodes
        services.AddSingleton<ILibraryRepository>(sp => new SqliteLibraryRepository(paths.LibraryDatabase, sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton(sp => new EpisodeService(sp.GetRequiredService<ILibraryRepository>(), sp.GetRequiredService<OuttaSettings>().EpisodeLength));
        services.AddSingleton(_ => new SourceRegistry());
        services.AddSingleton(_ => new ScraperRegistry());
        services.AddSingleton<IPrefixStore>(_ => new TomlPrefixStore(paths.PrefixesFile));
        services.AddSingleton<IPageFetcher>(_ => new HttpPageFetcher(HttpPageFetcher.CreateClient()));

        // Storage and uploads
        services.AddSingleton(_ => UnavailableCredentialStore.ForThisSystem());
        services.AddSingleton(sp => new Rclone(
            sp.GetRequiredService<IProcessRunner>(), sp.GetRequiredService<ToolLocator>(), sp.GetRequiredService<ICredentialStore>(), paths.RcloneConfig));
        services.AddSingleton<IStorageRegistry>(sp => new TomlStorageRegistry(
            paths.StorageFile,
            config => config.Provider == StorageProviders.LocalFolder
                ? new LocalFolderBackend(config)
                : new RcloneBackend(config, sp.GetRequiredService<Rclone>())));
        services.AddSingleton<UploadService>();
        services.AddSingleton<IClipUploader>(sp => sp.GetRequiredService<UploadService>());
        services.AddSingleton<IEpisodeUploader>(sp => sp.GetRequiredService<UploadService>());

        // Use cases
        services.AddSingleton(sp => new MakeClipHandler(
            sp.GetRequiredService<SourceRegistry>(), sp.GetRequiredService<IVideoResolver>(), sp.GetRequiredService<IClipDownloader>(),
            sp.GetRequiredService<IMediaProcessor>(), sp.GetRequiredService<ILibraryRepository>(), sp.GetRequiredService<EpisodeService>(),
            sp.GetRequiredService<OuttaSettings>(), sp.GetRequiredService<IReporter>(), sp.GetRequiredService<IClipUploader>()));
        services.AddSingleton(sp => new BuildEpisodeHandler(
            sp.GetRequiredService<EpisodeService>(), sp.GetRequiredService<IMediaProcessor>(), sp.GetRequiredService<OuttaSettings>(),
            paths.CacheDirectory, sp.GetRequiredService<IReporter>(), sp.GetRequiredService<IEpisodeUploader>()));
        services.AddSingleton<ScrapeHandler>();
        services.AddSingleton<IEnvironmentInspector, SystemEnvironmentInspector>();
        services.AddSingleton<DoctorService>();

        // Updates
        services.AddSingleton(_ => new GitHubReleases(GitHubReleases.CreateClient()));
        services.AddSingleton(sp => new SelfUpdater(
            sp.GetRequiredService<GitHubReleases>(), Environment.ProcessPath ?? "outta", sp.GetRequiredService<IReporter>()));
        services.AddSingleton<YtDlpInstaller>();
        return services;
    }
}
