using System.Globalization;
using OuttaContext.Core.Reporting;
using Spectre.Console;
using Spectre.Console.Rendering;

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

/// <summary>
/// A live Spectre progress bar. Spectre draws progress inside a callback, so the bar runs on a
/// background task until this object is disposed.
/// </summary>
internal sealed class LiveProgressTask : IProgressTask
{
    private readonly TaskCompletionSource<ProgressTask> _started = new();
    private readonly TaskCompletionSource _finished = new();
    private readonly Task _display;

    public LiveProgressTask(IAnsiConsole console, string description, ProgressUnit unit, double? total)
    {
        _display = console.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(Columns(unit))
            .StartAsync(async context =>
            {
                ProgressTask task = context.AddTask(description, maxValue: total ?? 1);
                task.IsIndeterminate = total is null;
                _started.SetResult(task);
                await _finished.Task;
                task.StopTask();
            });
    }

    public void Report(double completed, double? total = null)
    {
        ProgressTask task = _started.Task.GetAwaiter().GetResult();
        if (total is { } max && max > 0)
        {
            task.MaxValue = max;
            task.IsIndeterminate = false;
        }

        task.Value = Math.Min(completed, task.MaxValue);
    }

    public void Dispose()
    {
        _finished.TrySetResult();
        _display.GetAwaiter().GetResult();
    }

    private static ProgressColumn[] Columns(ProgressUnit unit) => unit switch
    {
        ProgressUnit.Bytes => [new SpinnerColumn(), new ProgressBarColumn(), new PercentageColumn(), new DownloadedColumn(), new TransferSpeedColumn(), new ElapsedTimeColumn()],
        ProgressUnit.Seconds => [new SpinnerColumn(), new ProgressBarColumn(), new PercentageColumn(), new CountColumn("{0:0.0}s of {1:0.0}s"), new ElapsedTimeColumn()],
        _ => [new SpinnerColumn(), new ProgressBarColumn(), new PercentageColumn(), new CountColumn("{0:0}/{1:0} pieces"), new ElapsedTimeColumn()],
    };

    /// <summary>Shows "done of total" with a format such as "{0:0.0}s of {1:0.0}s".</summary>
    private sealed class CountColumn(string format) : ProgressColumn
    {
        public override IRenderable Render(RenderOptions options, ProgressTask task, TimeSpan deltaTime) =>
            new Text(string.Format(CultureInfo.InvariantCulture, format, task.Value, task.MaxValue));
    }
}
