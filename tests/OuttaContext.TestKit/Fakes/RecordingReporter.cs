using OuttaContext.Core.Reporting;

namespace OuttaContext.TestKit.Fakes;

/// <summary>An <see cref="IReporter"/> that keeps every message so tests can check what the user would see.</summary>
public sealed class RecordingReporter : IReporter
{
    public List<(string Kind, string Text)> Messages { get; } = [];

    public List<RecordedProgress> ProgressBars { get; } = [];

    /// <summary>Pretend --verbose was given.</summary>
    public bool IsVerbose { get; set; }

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
