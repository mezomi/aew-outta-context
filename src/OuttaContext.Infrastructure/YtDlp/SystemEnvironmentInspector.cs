using System.Text.RegularExpressions;
using OuttaContext.Core.Doctor;
using OuttaContext.Infrastructure.Processes;

namespace OuttaContext.Infrastructure.YtDlp;

/// <summary>Answers the doctor's questions about this computer: programs on PATH and yt-dlp's abilities.</summary>
public sealed partial class SystemEnvironmentInspector(IProcessRunner runner, ToolLocator tools, TimeProvider clock) : IEnvironmentInspector
{
    public DateOnly Today => DateOnly.FromDateTime(clock.GetLocalNow().Date);

    public Maybe<string> FindProgram(string name) => tools.Find(name);

    public async Task<Maybe<string>> GetYtDlpVersionAsync(CancellationToken cancellationToken)
    {
        if (!tools.Find("yt-dlp").TryGetValue(out string? ytDlp))
        {
            return Maybe<string>.None;
        }

        ProcessResult result = await runner.RunAsync(new ProcessSpec(ytDlp, ["--version"]), cancellationToken);
        return result.Succeeded ? result.Output.Trim() : Maybe<string>.None;
    }

    public async Task<IReadOnlyList<string>> GetImpersonationClientsAsync(CancellationToken cancellationToken)
    {
        if (!tools.Find("yt-dlp").TryGetValue(out string? ytDlp))
        {
            return [];
        }

        ProcessResult result = await runner.RunAsync(
            new ProcessSpec(ytDlp, ["--ignore-config", "--list-impersonate-targets"]), cancellationToken);
        return ParseImpersonationClients(result.Output);
    }

    /// <summary>Client names from yt-dlp's target table, e.g. "Chrome-133  Macos-15  curl_cffi" → chrome.</summary>
    public static IReadOnlyList<string> ParseImpersonationClients(string output) =>
        output.Split('\n')
            .Select(line => TargetRowRegex().Match(line.Trim()))
            .Where(match => match.Success)
            .Select(match => match.Groups[1].Value.ToLowerInvariant())
            .Distinct()
            .ToList();

    [GeneratedRegex(@"^([A-Za-z]+)(?:-[\w.]+)?\s+\S+\s+curl_cffi$")]
    private static partial Regex TargetRowRegex();
}
