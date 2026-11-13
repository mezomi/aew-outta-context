using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace OuttaContext.Infrastructure.Processes;

/// <summary>Runs a program, streams its output line by line, and stops it if it stalls or is cancelled.</summary>
public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(ProcessSpec spec, CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = BuildStartInfo(spec), EnableRaisingEvents = true };
        var output = new StringBuilder();
        var error = new StringBuilder();
        long lastActivity = Stopwatch.GetTimestamp();

        if (!spec.Interactive)
        {
            process.OutputDataReceived += (_, line) => Collect(line.Data, output, spec.OnOutputLine);
            process.ErrorDataReceived += (_, line) => Collect(line.Data, error, spec.OnErrorLine);
        }

        try
        {
            process.Start();
        }
        catch (Win32Exception exception)
        {
            return new ProcessResult(-1, string.Empty, exception.Message) { NotFound = true };
        }

        if (!spec.Interactive)
        {
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            if (spec.StandardInput is { } input)
            {
                await process.StandardInput.WriteAsync(input.AsMemory(), cancellationToken);
                process.StandardInput.Close();
            }
        }

        bool stalled = false;
        try
        {
            while (!process.HasExited)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
                if (spec.StallTimeout is { } timeout && Stopwatch.GetElapsedTime(Interlocked.Read(ref lastActivity)) > timeout)
                {
                    stalled = true;
                    process.Kill(entireProcessTree: true);
                    break;
                }
            }

            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        lock (output)
        {
            lock (error)
            {
                return new ProcessResult(process.ExitCode, output.ToString(), error.ToString()) { Stalled = stalled };
            }
        }

        void Collect(string? line, StringBuilder sink, Action<string>? callback)
        {
            if (line is null)
            {
                return;
            }

            Interlocked.Exchange(ref lastActivity, Stopwatch.GetTimestamp());
            lock (sink)
            {
                sink.AppendLine(line);
            }

            callback?.Invoke(line);
        }
    }

    private static ProcessStartInfo BuildStartInfo(ProcessSpec spec)
    {
        var info = new ProcessStartInfo(spec.FileName)
        {
            UseShellExecute = false,
            RedirectStandardOutput = !spec.Interactive,
            RedirectStandardError = !spec.Interactive,
            RedirectStandardInput = !spec.Interactive && spec.StandardInput is not null,
            StandardOutputEncoding = spec.Interactive ? null : Encoding.UTF8,
            StandardErrorEncoding = spec.Interactive ? null : Encoding.UTF8,
            CreateNoWindow = !spec.Interactive,
        };
        foreach (string argument in spec.Arguments)
        {
            info.ArgumentList.Add(argument);
        }

        foreach ((string name, string value) in spec.Environment)
        {
            info.Environment[name] = value;
        }

        return info;
    }
}
