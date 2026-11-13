using OuttaContext.Core.Common;

namespace OuttaContext.Infrastructure.Updates;

/// <summary>Errors from `outta update` and GitHub.</summary>
public static class UpdateErrors
{
    public static Error NoRelease() => new("github.no_release", "No release has been published yet.", "Try again later.");

    public static Error HttpStatus(int status) =>
        new("github.http_status", $"GitHub answered with HTTP {status}.", "Try again in a few minutes.");

    public static Error EmptyAnswer() => new("github.empty", "GitHub's answer was empty.", "Try again in a few minutes.");

    public static Error Unreachable(string problem) => new("github.unreachable", $"Could not reach GitHub: {problem}", "Check your connection.");

    public static Error DownloadFailed(string problem) => new("github.download_failed", $"Download failed: {problem}", "Check your connection.");

    public static Error AssetMissing(string assetName) =>
        new("update.asset_missing", $"The latest release has no {assetName}.", "Try again later.");

    public static Error Corrupted() =>
        new("update.checksum", "The download was corrupted (checksum mismatch).", "Run `outta update` again.");

    public static Error CannotReplace(string path, string problem) =>
        new("update.replace", $"Could not replace {path}: {problem}", "Close other terminals running outta, then try again.");

    public static Error YtDlpSelfUpdateFailed(string details, string path) =>
        new("yt-dlp.update_failed", $"yt-dlp could not update itself: {details}", "Delete it and run `outta update` to download it again: " + path);

    public static Error YtDlpAssetMissing(string assetName) =>
        new("yt-dlp.asset_missing", $"yt-dlp's latest release has no {assetName}.", "Install yt-dlp yourself: `winget install yt-dlp.yt-dlp`.");
}
