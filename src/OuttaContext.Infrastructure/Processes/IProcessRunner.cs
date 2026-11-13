namespace OuttaContext.Infrastructure.Processes;

/// <summary>Runs outside programs. Faked in tests.</summary>
/// <remarks>
/// A program failing is normal and is reported in <see cref="ProcessResult"/>; only cancellation
/// (Ctrl+C) throws.
/// </remarks>
public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(ProcessSpec spec, CancellationToken cancellationToken);
}
