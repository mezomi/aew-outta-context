using OuttaContext.Core.Common;
using OuttaContext.Core.Reporting;
using OuttaContext.Infrastructure.Processes;

namespace OuttaContext.Infrastructure.Updates;

/// <summary>
/// Keeps yt-dlp available and current. outta downloads yt-dlp's official build (which includes
/// browser impersonation) into its own tools folder, and updates it with `yt-dlp -U`.
/// A yt-dlp you installed yourself (on PATH) is used but not changed.
/// </summary>
public sealed class YtDlpInstaller(GitHubReleases releases, ToolLocator tools, IProcessRunner runner, IReporter reporter)
{
    public const string Repository = "yt-dlp/yt-dlp";

    /// <summary>yt-dlp's standalone build for this operating system.</summary>
    public static string AssetName =>
        OperatingSystem.IsWindows() ? "yt-dlp.exe" : OperatingSystem.IsMacOS() ? "yt-dlp_macos" : "yt-dlp_linux";

    public async Task<UnitResult<Error>> InstallOrUpdateAsync(CancellationToken cancellationToken)
    {
        string managed = tools.ManagedPath("yt-dlp");
        if (File.Exists(managed))
        {
            reporter.Step("Updating yt-dlp");
            ProcessResult result = await runner.RunAsync(new ProcessSpec(managed, ["-U"]), cancellationToken);
            if (!result.Succeeded)
            {
                return UpdateErrors.YtDlpSelfUpdateFailed(result.ErrorTail(2), managed);
            }

            reporter.Success(result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim() ?? "yt-dlp is up to date");
            return UnitResult.Success<Error>();
        }

        if (tools.Find("yt-dlp").TryGetValue(out string? own))
        {
            reporter.Success($"Using your own yt-dlp at {own}. Keep it updated yourself (e.g. `yt-dlp -U`).");
            return UnitResult.Success<Error>();
        }

        reporter.Step("Downloading yt-dlp");
        Result<Release, Error> latest = await releases.LatestAsync(Repository, cancellationToken);
        if (latest.IsFailure)
        {
            return latest.Error;
        }

        if (!latest.Value.Asset(AssetName).TryGetValue(out ReleaseAsset? asset))
        {
            return UpdateErrors.YtDlpAssetMissing(AssetName);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(managed)!);
        UnitResult<Error> download;
        using (IProgressTask progress = reporter.StartProgress(asset.Name, ProgressUnit.Bytes, asset.Size))
        {
            download = await releases.DownloadAsync(asset.DownloadUrl, managed, progress.Report, cancellationToken);
        }

        if (download.IsFailure)
        {
            return download.Error;
        }

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(managed, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        reporter.Success($"Installed yt-dlp {latest.Value.Tag} to {managed}");
        return UnitResult.Success<Error>();
    }
}
