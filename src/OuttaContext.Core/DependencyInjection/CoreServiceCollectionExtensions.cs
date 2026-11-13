using Microsoft.Extensions.DependencyInjection;
using OuttaContext.Core.Clips;
using OuttaContext.Core.Doctor;
using OuttaContext.Core.Episodes;
using OuttaContext.Core.Media;
using OuttaContext.Core.Scraping;
using OuttaContext.Core.Sources;
using OuttaContext.Core.Uploads;

namespace OuttaContext.Core.DependencyInjection;

public static class CoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the use cases and domain services. They need the adapters from
    /// <c>AddOuttaInfrastructure</c> and an <see cref="Reporting.IReporter"/> from the CLI.
    /// </summary>
    public static IServiceCollection AddOuttaCore(this IServiceCollection services)
    {
        services.AddSingleton(SourceRegistry.Default);
        services.AddSingleton(ScraperRegistry.Default);
        services.AddSingleton(new NormalizeTarget());
        services.AddSingleton<EpisodeCache>();
        services.AddSingleton<EpisodeService>();
        services.AddSingleton<UploadService>();
        services.AddSingleton<IClipUploader>(provider => provider.GetRequiredService<UploadService>());
        services.AddSingleton<IEpisodeUploader>(provider => provider.GetRequiredService<UploadService>());
        services.AddSingleton<MakeClipHandler>();
        services.AddSingleton<BuildEpisodeHandler>();
        services.AddSingleton<ScrapeHandler>();
        services.AddSingleton<DoctorService>();
        return services;
    }
}
