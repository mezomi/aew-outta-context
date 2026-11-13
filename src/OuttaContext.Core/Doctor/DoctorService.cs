using System.Globalization;
using OuttaContext.Core.Common;
using OuttaContext.Core.Media;
using OuttaContext.Core.Settings;
using OuttaContext.Core.Storage;

namespace OuttaContext.Core.Doctor;

/// <summary>`outta doctor`: checks outside programs, folders and storage logins.</summary>
public sealed class DoctorService(
    IMediaProcessor media,
    IEnvironmentInspector environment,
    OuttaSettings settings,
    IStorageRegistry storages,
    IStorageBackendFactory backends,
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
        bool needsRclone = configured.Any(config => config.Provider.UsesRclone);
        checks.Add(CheckRclone(needsRclone));

        if (testStorage)
        {
            foreach (StorageConfig config in configured)
            {
                UnitResult<Error> test = await backends.Create(config).TestAsync(cancellationToken);
                checks.Add(test.IsSuccess
                    ? new DoctorCheck($"storage {config.Name}", CheckState.Ok, $"{config.Provider.Label} works")
                    : new DoctorCheck($"storage {config.Name}", CheckState.Fail, test.Error.Message, test.Error.Hint));
            }
        }

        return checks;
    }

    public static Maybe<int> YtDlpAgeDays(string version, DateOnly today)
    {
        string[] parts = version.Split('.');
        return parts.Length >= 3
            && int.TryParse(parts[0], CultureInfo.InvariantCulture, out int year)
            && int.TryParse(parts[1], CultureInfo.InvariantCulture, out int month)
            && int.TryParse(parts[2], CultureInfo.InvariantCulture, out int day)
            && DateOnly.TryParseExact($"{year:0000}-{month:00}-{day:00}", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly released)
            ? today.DayNumber - released.DayNumber
            : Maybe<int>.None;
    }

    private async Task<DoctorCheck> CheckFfmpegAsync(CancellationToken cancellationToken)
    {
        if (!media.IsAvailable)
        {
            return new("ffmpeg", CheckState.Fail, "ffmpeg/ffprobe not found", "Install it: `winget install ffmpeg`, then open a new terminal.");
        }

        Maybe<string> versionLine = await media.GetVersionAsync(cancellationToken);
        string version = versionLine
            .Map(line => line.Replace("ffmpeg version ", string.Empty, StringComparison.Ordinal).Split(" Copyright")[0])
            .GetValueOrDefault("found");
        return new("ffmpeg", CheckState.Ok, version);
    }

    private async Task<DoctorCheck> CheckYtDlpAsync(CancellationToken cancellationToken)
    {
        Maybe<string> installed = await environment.GetYtDlpVersionAsync(cancellationToken);
        if (!installed.TryGetValue(out string? version))
        {
            return new("yt-dlp", CheckState.Fail, "yt-dlp is not installed", "Run `outta update` to download it.");
        }

        return YtDlpAgeDays(version, environment.Today).TryGetValue(out int age) && age > YtDlpMaxAgeDays
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
            if (environment.FindProgram(program).HasValue)
            {
                return new("js runtime", CheckState.Ok, $"{program} (used by yt-dlp for YouTube)");
            }
        }

        return new("js runtime", CheckState.Warn, "no deno/node found; some YouTube videos may fail", "Install Deno: `winget install DenoLand.Deno`.");
    }

    private DoctorCheck CheckRclone(bool required)
    {
        if (environment.FindProgram("rclone").HasValue)
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
        KeychainInfo keychain = credentials.Describe();
        return keychain.Available
            ? new("keychain", CheckState.Ok, keychain.Name)
            : new("keychain", CheckState.Warn, $"no system keychain ({keychain.Name})", "Cloud storage logins need Windows Credential Manager.");
    }
}
