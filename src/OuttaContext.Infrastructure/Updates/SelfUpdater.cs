using System.Security.Cryptography;
using OuttaContext.Core.Reporting;
using OuttaContext.Core.Results;

namespace OuttaContext.Infrastructure.Updates;

/// <summary>
/// `outta update`: replaces the running outta.exe with the newest release build.
/// A running .exe can't be overwritten on Windows, but it can be renamed: the new file is
/// downloaded next to it, the old one becomes outta.exe.old, and the new one takes its place.
/// The .old file is removed the next time outta starts.
/// </summary>
public sealed class SelfUpdater(GitHubReleases releases, string executablePath, IReporter reporter)
{
    public const string Repository = "mezomi/aew-outta-context";
    public const string AssetName = "outta.exe";

    /// <summary>True when running as the published single-file exe (not from a development build).</summary>
    public static bool IsPublishedExecutable => string.IsNullOrEmpty(typeof(SelfUpdater).Assembly.Location);

    public static void RemoveLeftover(string executablePath)
    {
        try
        {
            File.Delete(executablePath + ".old");
        }
        catch (IOException)
        {
            // Still locked right after an update; the next start removes it.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Returns true if outta.exe was replaced.</summary>
    public async Task<ToolResult<bool>> UpdateAsync(bool checkOnly, CancellationToken cancellationToken)
    {
        reporter.Step("Checking for a newer outta.exe...");
        ToolResult<Release> latest = await releases.LatestAsync(Repository, cancellationToken);
        if (latest.Failed)
        {
            return latest.Error;
        }

        if (latest.Value.Assets.FirstOrDefault(asset => asset.Name == AssetName) is not { } asset)
        {
            return new ToolError(GitHubReleases.Tool, $"The latest release has no {AssetName}.", "Try again later.");
        }

        string tag = latest.Value.Tag;
        string expected = asset.Digest?.Replace("sha256:", string.Empty, StringComparison.Ordinal) ?? string.Empty;
        if (expected.Length > 0 && expected == await HashAsync(executablePath, cancellationToken))
        {
            reporter.Success($"outta is up to date ({tag})");
            return ToolResult<bool>.Success(false);
        }

        if (checkOnly)
        {
            reporter.Success($"An update is available ({tag}). Run `outta update` to install it.");
            return ToolResult<bool>.Success(false);
        }

        string downloaded = executablePath + ".new";
        reporter.Step("Downloading the new outta.exe");
        ToolResult<Done> download;
        using (IProgressTask progress = reporter.StartProgress(AssetName, ProgressUnit.Bytes, asset.Size))
        {
            download = await releases.DownloadAsync(asset.DownloadUrl, downloaded, progress.Report, cancellationToken);
        }

        if (download.Failed)
        {
            return download.Error;
        }

        string actual = await HashAsync(downloaded, cancellationToken);
        if (expected.Length > 0 && actual != expected)
        {
            File.Delete(downloaded);
            return new ToolError(GitHubReleases.Tool, "The download was corrupted (checksum mismatch).", "Run `outta update` again.");
        }

        if (actual == await HashAsync(executablePath, cancellationToken))
        {
            File.Delete(downloaded);
            reporter.Success($"outta is up to date ({tag})");
            return ToolResult<bool>.Success(false);
        }

        string old = executablePath + ".old";
        try
        {
            File.Delete(old);
            File.Move(executablePath, old);
            File.Move(downloaded, executablePath);
            if (!OperatingSystem.IsWindows())
            {
                // Keep the "executable" permission on Linux/macOS.
                File.SetUnixFileMode(executablePath, File.GetUnixFileMode(old));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            if (!File.Exists(executablePath) && File.Exists(old))
            {
                File.Move(old, executablePath);
            }

            File.Delete(downloaded);
            return new ToolError(GitHubReleases.Tool, $"Could not replace {executablePath}: {exception.Message}", "Close other terminals running outta, then try again.");
        }

        reporter.Success($"Updated outta.exe to the latest build ({tag}). Your data is unchanged.");
        return ToolResult<bool>.Success(true);
    }

    private static async Task<string> HashAsync(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
    }
}
