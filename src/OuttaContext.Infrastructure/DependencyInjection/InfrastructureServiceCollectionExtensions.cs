using Microsoft.Extensions.DependencyInjection;
using OuttaContext.Core.Clips;
using OuttaContext.Core.Common;
using OuttaContext.Core.Doctor;
using OuttaContext.Core.Library;
using OuttaContext.Core.Media;
using OuttaContext.Core.Paths;
using OuttaContext.Core.Reporting;
using OuttaContext.Core.Scraping;
using OuttaContext.Core.Settings;
using OuttaContext.Core.Storage;
using OuttaContext.Infrastructure.Credentials;
using OuttaContext.Infrastructure.FFmpeg;
using OuttaContext.Infrastructure.Media;
using OuttaContext.Infrastructure.Persistence;
using OuttaContext.Infrastructure.Processes;
using OuttaContext.Infrastructure.Storage;
using OuttaContext.Infrastructure.Updates;
using OuttaContext.Infrastructure.Web;
using OuttaContext.Infrastructure.YtDlp;

namespace OuttaContext.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the adapters behind Core's interfaces (yt-dlp, ffmpeg, rclone, SQLite, TOML,
    /// the keychain, HTTP) and the settings loaded from <paramref name="paths"/>.
    /// </summary>
    public static IServiceCollection AddOuttaInfrastructure(this IServiceCollection services, AppPaths paths)
    {
        services.AddSingleton(paths);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(RunningExecutable.Current());

        // Settings: loaded once per run. A broken settings.toml stops every command except
        // `outta config`, which reads the store itself, so it throws here (and is printed as an error).
        services.AddSingleton<ISettingsStore, TomlSettingsStore>();
        services.AddSingleton(provider =>
            provider.GetRequiredService<ISettingsStore>().Load().ValueOrThrow());
        services.AddSingleton(provider => new YtDlpOptions(provider.GetRequiredService<IReporter>())
        {
            CookiesFromBrowser = provider.GetRequiredService<OuttaSettings>().CookiesFromBrowser,
        });

        // Programs
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<ToolLocator>();
        services.AddSingleton<IVideoResolver, YtDlpVideoResolver>();
        services.AddSingleton<IStreamFetcher, YtDlpStreamFetcher>();
        services.AddSingleton<IClipDownloader, ClipDownloader>();
        services.AddSingleton<IMediaProcessor, FfmpegMediaProcessor>();
        services.AddSingleton<IEnvironmentInspector, SystemEnvironmentInspector>();

        // Files and the keychain
        services.AddSingleton<ILibraryRepository>(provider =>
            new SqliteLibraryRepository(paths.LibraryDatabase, provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IPrefixStore, TomlPrefixStore>();
        services.AddSingleton<IStorageRegistry, TomlStorageRegistry>();
        services.AddSingleton(_ => UnavailableCredentialStore.ForThisSystem());
        services.AddSingleton<Rclone>();
        services.AddSingleton<IStorageBackendFactory, StorageBackendFactory>();

        // HTTP (typed clients from IHttpClientFactory)
        services.AddHttpClient<IPageFetcher, HttpPageFetcher>(HttpPageFetcher.Configure)
            .ConfigurePrimaryHttpMessageHandler(HttpPageFetcher.CreateHandler);
        services.AddHttpClient<GitHubReleases>(GitHubReleases.Configure);
        services.AddTransient<SelfUpdater>();
        services.AddTransient<YtDlpInstaller>();
        return services;
    }
}
