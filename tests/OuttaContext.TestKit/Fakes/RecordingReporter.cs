using OuttaContext.Core.Reporting;

namespace OuttaContext.TestKit.Fakes;

/// <summary>An <see cref="IReporter"/> that keeps every message so tests can check what the user would see.</summary>
public sealed class RecordingReporter : IReporter
{
    public List<(string Kind, string Text)> Messages { get; } = [];

    public List<RecordedProgress> ProgressBars { get; } = [];

    public void Step(string message) => Messages.Add(("step", message));

    public void Success(string message) => Messages.Add(("success", message));

    public void Warn(string message) => Messages.Add(("warn", message));

    public void Detail(string message) => Messages.Add(("detail", message));

    public void Info(string message) => Messages.Add(("info", message));

    public IProgressTask StartProgress(string description, ProgressUnit unit, double? total = null)
    {
        var progress = new RecordedProgress(description, unit, total);
        ProgressBars.Add(progress);
        return progress;
    }

    /// <summary>The texts of one kind of message: "step", "success", "warn", "detail" or "info".</summary>
    public IReadOnlyList<string> Texts(string kind) => Messages.Where(m => m.Kind == kind).Select(m => m.Text).ToList();
}

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
