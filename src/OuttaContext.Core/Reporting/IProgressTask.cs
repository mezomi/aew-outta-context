namespace OuttaContext.Core.Reporting;

/// <summary>One running progress bar. Dispose it when the work is done.</summary>
public interface IProgressTask : IDisposable
{
    void Report(double completed, double? total = null);
}
