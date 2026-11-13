using OuttaContext.Cli.Composition;
using OuttaContext.Core.Paths;
using OuttaContext.Infrastructure.Updates;
using Spectre.Console;

SelfUpdater.RemoveLeftover(RunningExecutable.Current().Path);

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

IAnsiConsole console = AnsiConsole.Console;
if (Console.IsOutputRedirected)
{
    // Piped to a file or CI log: there is no window width, so use COLUMNS or a sensible default.
    console.Profile.Width = int.TryParse(Environment.GetEnvironmentVariable("COLUMNS"), out int columns) && columns > 20 ? columns : 120;
}

var services = OuttaServices.Create(console, AppPaths.FromEnvironment());
return await OuttaApp.Create(services).RunAsync(GlobalOptions.MoveAfterCommand(args), cancellation.Token);
