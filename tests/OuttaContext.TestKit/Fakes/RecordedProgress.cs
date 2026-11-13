using OuttaContext.Core.Reporting;

namespace OuttaContext.TestKit.Fakes;

/// <summary>A progress bar that records every update.</summary>
public sealed class RecordedProgress(string description, ProgressUnit unit, double? total) : IProgressTask
{
    public string Description { get; } = description;

    public ProgressUnit Unit { get; } = unit;

    public double? Total { get; } = total;

    public List<(double Completed, double? Total)> Updates { get; } = [];

    public bool Disposed { get; private set; }

    public void Report(double completed, double? total = null) => Updates.Add((completed, total));

    public void Dispose() => Disposed = true;
}
