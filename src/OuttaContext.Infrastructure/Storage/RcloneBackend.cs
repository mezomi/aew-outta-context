using System.Text.Json;
using OuttaContext.Core.Common;
using OuttaContext.Core.Storage;

namespace OuttaContext.Infrastructure.Storage;

/// <summary>Google Drive, OneDrive, Dropbox and Backblaze B2, all through rclone.</summary>
public sealed class RcloneBackend(StorageConfig config, Rclone rclone) : ISignInStorage
{
    private static readonly Dictionary<StorageProvider, string> RcloneTypes = new()
    {
        [StorageProvider.GoogleDrive] = "drive",
        [StorageProvider.OneDrive] = "onedrive",
        [StorageProvider.Dropbox] = "dropbox",
        [StorageProvider.Backblaze] = "b2",
    };

    private static readonly string[] LoginProblemWords = ["token", "unauthor", "401", "403", "invalid_grant"];

    public StorageConfig Config => config;

    public string Remote => Rclone.RemoteName(config.Name);

    private bool UsesBrowserSignIn => config.Provider.SignsInWithBrowser;

    public static string SecretKey(StorageName storageName, string option) => $"storage:{storageName}:{option}";

    public string Target(string subfolder = "", string fileName = "") =>
        $"{Remote}:{string.Join('/', new[] { config.Folder, subfolder, fileName }.Select(part => part.Trim('/')).Where(part => part.Length > 0))}";

    /// <summary>
    /// Backblaze B2 is defined entirely by environment variables, so its key never touches a file.
    /// </summary>
    public Dictionary<string, string> RemoteEnvironment()
    {
        var environment = new Dictionary<string, string>();
        if (config.Provider == StorageProvider.Backblaze)
        {
            string prefix = $"RCLONE_CONFIG_{Remote.ToUpperInvariant()}_";
            environment[prefix + "TYPE"] = "b2";
            environment[prefix + "ACCOUNT"] = config.Option(StorageConfig.AccountOption).GetValueOrDefault(string.Empty);
            rclone.Credentials.Get(SecretKey(config.Name, "key")).Tap(key => environment[prefix + "KEY"] = key);
        }

        return environment;
    }

    public async Task<UnitResult<Error>> SignInAsync(Func<string, string> askSecret, CancellationToken cancellationToken)
    {
        if (!rclone.IsAvailable)
        {
            return StorageBackendErrors.RcloneMissing();
        }

        if (!UsesBrowserSignIn)
        {
            string key = askSecret("Backblaze application key");
            if (key.Length == 0)
            {
                return StorageBackendErrors.NoKeyEntered();
            }

            rclone.Credentials.Set(SecretKey(config.Name, "key"), key);
            return UnitResult.Success<Error>();
        }

        var arguments = new List<string> { "config", "create", Remote, RcloneTypes[config.Provider] };
        if (config.Provider == StorageProvider.GoogleDrive)
        {
            arguments.Add("scope=drive");
        }

        // Interactive: rclone opens the browser for the sign-in and talks to the user directly.
        var result = await rclone.RunAsync(arguments, cancellationToken, interactive: true);
        if (!result.Succeeded)
        {
            return StorageBackendErrors.SignInUnfinished(config);
        }

        await rclone.EnsureEncryptedAsync(cancellationToken);
        return UnitResult.Success<Error>();
    }

    public async Task<UnitResult<Error>> TestAsync(CancellationToken cancellationToken)
    {
        if (!rclone.IsAvailable)
        {
            return StorageBackendErrors.RcloneMissing();
        }

        string probe = $".outta-test-{Guid.NewGuid():N}.txt";
        Dictionary<string, string> environment = RemoteEnvironment();
        foreach ((string[] arguments, string? input) in new (string[], string?)[]
        {
            (["mkdir", Target()], null),
            (["rcat", Target(fileName: probe)], "ok\n"),
            (["deletefile", Target(fileName: probe)], null),
        })
        {
            var result = await rclone.RunAsync(arguments, cancellationToken, environment, input);
            if (!result.Succeeded)
            {
                return StorageBackendErrors.TestFailed(config, result.ErrorTail(3));
            }
        }

        return UnitResult.Success<Error>();
    }

    public async Task<Result<UploadedFile, Error>> UploadAsync(
        string localPath, string subfolder, Action<double, double?> onProgressBytes, CancellationToken cancellationToken)
    {
        if (!rclone.IsAvailable)
        {
            return StorageBackendErrors.RcloneMissing();
        }

        string target = Target(subfolder, Path.GetFileName(localPath));
        double total = new FileInfo(localPath).Length;
        var errors = new List<string>();
        var result = await rclone.RunAsync(
            ["copyto", localPath, target, "--use-json-log", "--stats", "1s", "-v"],
            cancellationToken,
            RemoteEnvironment(),
            onErrorLine: line => ReadLogLine(line, total, onProgressBytes, errors));

        if (!result.Succeeded)
        {
            string message = errors.LastOrDefault() ?? $"exit code {result.ExitCode}";
            bool loginProblem = LoginProblemWords
                .Any(word => message.Contains(word, StringComparison.OrdinalIgnoreCase));
            return StorageBackendErrors.UploadFailed(config, message, loginProblem);
        }

        onProgressBytes(total, total);
        return new UploadedFile(target);
    }

    public async Task<UnitResult<Error>> ForgetAsync(CancellationToken cancellationToken)
    {
        if (UsesBrowserSignIn && rclone.IsAvailable)
        {
            await rclone.RunAsync(["config", "delete", Remote], cancellationToken);
        }

        rclone.Credentials.Delete(SecretKey(config.Name, "key"));
        return UnitResult.Success<Error>();
    }

    /// <summary>rclone's JSON log: "stats" lines carry progress, error lines carry the reason.</summary>
    private static void ReadLogLine(string line, double total, Action<double, double?> onProgressBytes, List<string> errors)
    {
        try
        {
            using JsonDocument entry = JsonDocument.Parse(line);
            JsonElement root = entry.RootElement;
            if (root.TryGetProperty("stats", out JsonElement stats))
            {
                double bytes = stats.TryGetProperty("bytes", out JsonElement b) ? b.GetDouble() : 0;
                double size = stats.TryGetProperty("totalBytes", out JsonElement t) && t.GetDouble() > 0 ? t.GetDouble() : total;
                onProgressBytes(bytes, size);
            }

            if (root.TryGetProperty("level", out JsonElement level) && level.GetString() is "error" or "critical")
            {
                errors.Add(root.TryGetProperty("msg", out JsonElement message) ? message.GetString() ?? string.Empty : line);
            }
        }
        catch (JsonException)
        {
            if (line.Trim().Length > 0)
            {
                errors.Add(line.Trim());
            }
        }
    }
}
