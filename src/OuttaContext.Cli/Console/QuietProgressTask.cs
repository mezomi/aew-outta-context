using OuttaContext.Core.Reporting;

namespace OuttaContext.Cli.Console;

/// <summary>Used with --quiet or when output isn't a terminal: progress is not drawn.</summary>
internal sealed class QuietProgressTask : IProgressTask
{
    public void Report(double completed, double? total = null)
    {
    }

    public void Dispose()
    {
    }
}
