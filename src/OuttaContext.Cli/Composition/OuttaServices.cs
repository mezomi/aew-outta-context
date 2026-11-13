using Microsoft.Extensions.DependencyInjection;
using OuttaContext.Core.DependencyInjection;
using OuttaContext.Core.Paths;
using OuttaContext.Infrastructure.DependencyInjection;
using Spectre.Console;

namespace OuttaContext.Cli.Composition;

/// <summary>
/// All of outta's services, from the three projects. Singletons are created on first use, so
/// `--help` doesn't touch the database or the network. Tests replace pieces with fakes afterwards.
/// </summary>
public static class OuttaServices
{
    public static IServiceCollection Create(IAnsiConsole console, AppPaths paths) =>
        new ServiceCollection()
            .AddOuttaInfrastructure(paths)
            .AddOuttaCore()
            .AddOuttaCli(console);
}
