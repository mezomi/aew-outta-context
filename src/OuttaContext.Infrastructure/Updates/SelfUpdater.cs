using System.Security.Cryptography;
using OuttaContext.Core.Common;
using OuttaContext.Core.Reporting;

namespace OuttaContext.Infrastructure.Updates;

/// <summary>
/// `outta update`: replaces the running outta.exe with the newest release build.
/// A running .exe can't be overwritten on Windows, but it can be renamed: the new file is
/// downloaded next to it, the old one becomes outta.exe.old, and the new one takes its place.
/// The .old file is removed the next time outta starts.
/// </summary>
public sealed class SelfUpdater(GitHubReleases releases, RunningExecutable executable, IReporter reporter)
{
    public const string Repository = "mezomi/aew-outta-context";
    public const string AssetName = "outta.exe";

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

    public async Task<Result<UpdateOutcome, Error>> UpdateAsync(bool checkOnly, CancellationToken cancellationToken)
    {
        string executablePath = executable.Path;
        reporter.Step("Checking for a newer outta.exe...");
        Result<Release, Error> latest = await releases.LatestAsync(Repository, cancellationToken);
        if (latest.IsFailure)
        {
            return latest.Error;
        }

        if (!latest.Value.Asset(AssetName).TryGetValue(out ReleaseAsset? asset))
        {
            return UpdateErrors.AssetMissing(AssetName);
        }

        string tag = latest.Value.Tag;
        string expected = asset.Sha256.GetValueOrDefault(string.Empty);
        if (expected.Length > 0 && expected == await HashAsync(executablePath, cancellationToken))
        {
            reporter.Success($"outta is up to date ({tag})");
            return UpdateOutcome.AlreadyUpToDate;
        }

        if (checkOnly)
        {
            reporter.Success($"An update is available ({tag}). Run `outta update` to install it.");
            return UpdateOutcome.UpdateAvailable;
        }

        string downloaded = executablePath + ".new";
        reporter.Step("Downloading the new outta.exe");
        UnitResult<Error> download;
        using (IProgressTask progress = reporter.StartProgress(AssetName, ProgressUnit.Bytes, asset.Size))
        {
            download = await releases.DownloadAsync(asset.DownloadUrl, downloaded, progress.Report, cancellationToken);
        }

        if (download.IsFailure)
        {
            return download.Error;
        }

        string actual = await HashAsync(downloaded, cancellationToken);
        if (expected.Length > 0 && actual != expected)
        {
            File.Delete(downloaded);
            return UpdateErrors.Corrupted();
        }

        if (actual == await HashAsync(executablePath, cancellationToken))
        {
            File.Delete(downloaded);
            reporter.Success($"outta is up to date ({tag})");
            return UpdateOutcome.AlreadyUpToDate;
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
            return UpdateErrors.CannotReplace(executablePath, exception.Message);
        }

        reporter.Success($"Updated outta.exe to the latest build ({tag}). Your data is unchanged.");
        return UpdateOutcome.Updated;
    }

    private static async Task<string> HashAsync(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
    }
}
