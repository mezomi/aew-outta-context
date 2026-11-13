using OuttaContext.Cli.Commands;
using OuttaContext.Cli.Console;
using OuttaContext.Core.Reporting;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Composition;

/// <summary>Runs before every command: applies --verbose / --quiet to the reporter.</summary>
public sealed class VerbosityInterceptor(SpectreReporter reporter) : ICommandInterceptor
{
    public void Intercept(CommandContext context, CommandSettings settings)
    {
        if (settings is GlobalSettings global)
        {
            reporter.Verbosity = global.Verbose ? Verbosity.Verbose : global.Quiet ? Verbosity.Quiet : Verbosity.Normal;
        }
    }

    public void InterceptResult(CommandContext context, CommandSettings settings, ref int result)
    {
    }
}
