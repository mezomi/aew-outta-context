using System.Globalization;
using OuttaContext.Core.Media;
using OuttaContext.Core.Results;
using OuttaContext.Core.Settings;
using OuttaContext.Core.Storage;

namespace OuttaContext.Core.Doctor;

public enum CheckState
{
    Ok,
    Warn,
    Fail,
}

/// <summary>One line of `outta doctor`: what was checked, how it went, and how to fix it.</summary>
public sealed record DoctorCheck(string Name, CheckState State, string Message, string Fix = "");

/// <summary>Things about this computer the doctor needs to look up.</summary>
public interface IEnvironmentInspector
{
    /// <summary>Full path of a program on PATH (or downloaded by the app), or null.</summary>
    string? FindProgram(string name);

    /// <summary>yt-dlp's version, e.g. "2026.10.26", or null if it isn't installed.</summary>
    Task<string?> GetYtDlpVersionAsync(CancellationToken cancellationToken);

    /// <summary>The browsers yt-dlp can pose as (e.g. chrome, firefox).</summary>
    Task<IReadOnlyList<string>> GetImpersonationClientsAsync(CancellationToken cancellationToken);

    DateOnly Today { get; }
}

/// <summary>`outta doctor`: checks outside programs, folders and storage logins.</summary>
public sealed class DoctorService(
    IMediaProcessor media,
    IEnvironmentInspector environment,
    OuttaSettings settings,
    IStorageRegistry storages,
    ICredentialStore credentials)
{
    public const int YtDlpMaxAgeDays = 31;
    public const string UpdateHint = "Run `outta update` (it also updates yt-dlp).";

    public async Task<IReadOnlyList<DoctorCheck>> RunAsync(bool testStorage, CancellationToken cancellationToken)
    {
        var checks = new List<DoctorCheck>
        {
            new("outta", CheckState.Ok, typeof(DoctorService).Assembly.GetName().Version?.ToString(3) ?? "?"),
            await CheckFfmpegAsync(cancellationToken),
            await CheckYtDlpAsync(cancellationToken),
            await CheckImpersonationAsync(cancellationToken),
            CheckJsRuntime(),
            CheckClipFolder(),
            CheckKeychain(),
        };

        IReadOnlyList<StorageConfig> configured = storages.List();
        bool needsRclone = configured.Any(config => StorageProviders.UsesRclone(config.Provider));
        checks.Add(CheckRclone(needsRclone));

        if (testStorage)
        {
            foreach (StorageConfig config in configured)
            {
                ToolResult<Done> test = await storages.Backend(config).TestAsync(cancellationToken);
                checks.Add(test.Succeeded
                    ? new DoctorCheck($"storage {config.Name}", CheckState.Ok, $"{config.Provider} works")
                    : new DoctorCheck($"storage {config.Name}", CheckState.Fail, test.Error.Message, test.Error.Hint));
            }
        }

        return checks;
    }

    public static int? YtDlpAgeDays(string version, DateOnly today)
    {
        string[] parts = version.Split('.');
        return parts.Length >= 3
            && int.TryParse(parts[0], CultureInfo.InvariantCulture, out int year)
            && int.TryParse(parts[1], CultureInfo.InvariantCulture, out int month)
            && int.TryParse(parts[2], CultureInfo.InvariantCulture, out int day)
            ? today.DayNumber - new DateOnly(year, month, day).DayNumber
            : null;
    }

    private async Task<DoctorCheck> CheckFfmpegAsync(CancellationToken cancellationToken)
    {
        if (!media.IsAvailable)
        {
            return new("ffmpeg", CheckState.Fail, "ffmpeg/ffprobe not found", "Install it: `winget install ffmpeg`, then open a new terminal.");
        }

        string version = await media.GetVersionAsync(cancellationToken) ?? "found";
        version = version.Replace("ffmpeg version ", string.Empty, StringComparison.Ordinal).Split(" Copyright")[0];
        return new("ffmpeg", CheckState.Ok, version);
    }

    private async Task<DoctorCheck> CheckYtDlpAsync(CancellationToken cancellationToken)
    {
        string? version = await environment.GetYtDlpVersionAsync(cancellationToken);
        if (version is null)
        {
            return new("yt-dlp", CheckState.Fail, "yt-dlp is not installed", "Run `outta update` to download it.");
        }

        return YtDlpAgeDays(version, environment.Today) is { } age && age > YtDlpMaxAgeDays
            ? new("yt-dlp", CheckState.Warn, $"{version} is {age} days old; sites change often", UpdateHint)
            : new("yt-dlp", CheckState.Ok, version);
    }

    private async Task<DoctorCheck> CheckImpersonationAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<string> clients = await environment.GetImpersonationClientsAsync(cancellationToken);
        return clients.Contains("chrome") && clients.Contains("firefox")
            ? new("impersonation", CheckState.Ok, $"yt-dlp can act as a browser ({string.Join(", ", clients)})")
            : new("impersonation", CheckState.Warn, "yt-dlp can't act as Chrome/Firefox; Dailymotion may refuse downloads", UpdateHint);
    }

    private DoctorCheck CheckJsRuntime()
    {
        foreach (string program in new[] { "deno", "node", "bun" })
        {
            if (environment.FindProgram(program) is not null)
            {
                return new("js runtime", CheckState.Ok, $"{program} (used by yt-dlp for YouTube)");
            }
        }

        return new("js runtime", CheckState.Warn, "no deno/node found; some YouTube videos may fail", "Install Deno: `winget install DenoLand.Deno`.");
    }

    private DoctorCheck CheckRclone(bool required)
    {
        if (environment.FindProgram("rclone") is not null)
        {
            return new("rclone", CheckState.Ok, "found");
        }

        return new(
            "rclone",
            required ? CheckState.Fail : CheckState.Warn,
            "rclone not found (needed for Drive/OneDrive/Dropbox/B2)",
            "Install it: `winget install Rclone.Rclone`, then open a new terminal.");
    }

    private DoctorCheck CheckClipFolder()
    {
        string folder = settings.ClipFolder;
        try
        {
            Directory.CreateDirectory(folder);
            string probe = Path.Combine(folder, $".outta-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new("clip folder", CheckState.Fail, $"{folder}: {exception.Message}", "Pick another folder: `outta config set-destination FOLDER`.");
        }

        double freeGb = new DriveInfo(Path.GetFullPath(folder)).AvailableFreeSpace / 1e9;
        return new("clip folder", freeGb < 2 ? CheckState.Warn : CheckState.Ok, $"{folder} ({freeGb:0.0} GB free)");
    }

    private DoctorCheck CheckKeychain()
    {
        (bool available, string name) = credentials.Describe();
        return available
            ? new("keychain", CheckState.Ok, name)
            : new("keychain", CheckState.Warn, $"no system keychain ({name})", "Cloud storage logins need Windows Credential Manager.");
    }
}
