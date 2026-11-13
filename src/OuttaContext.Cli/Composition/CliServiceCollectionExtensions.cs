using Microsoft.Extensions.DependencyInjection;
using OuttaContext.Cli.Console;
using OuttaContext.Core.Reporting;
using Spectre.Console;

namespace OuttaContext.Cli.Composition;

public static class CliServiceCollectionExtensions
{
    /// <summary>Registers the console and the one reporter every command, the error handler and --verbose share.</summary>
    public static IServiceCollection AddOuttaCli(this IServiceCollection services, IAnsiConsole console)
    {
        services.AddSingleton(console);
        services.AddSingleton(new SpectreReporter(console));
        services.AddSingleton<IReporter>(provider => provider.GetRequiredService<SpectreReporter>());
        return services;
    }
}
