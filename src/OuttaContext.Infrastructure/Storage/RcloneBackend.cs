using System.Text.Json;
using OuttaContext.Core.Results;
using OuttaContext.Core.Storage;

namespace OuttaContext.Infrastructure.Storage;

/// <summary>Google Drive, OneDrive, Dropbox and Backblaze B2, all through rclone.</summary>
public sealed class RcloneBackend(StorageConfig config, Rclone rclone) : ISignInStorage
{
    private static readonly Dictionary<string, string> RcloneTypes = new()
    {
        [StorageProviders.GoogleDrive] = "drive",
        [StorageProviders.OneDrive] = "onedrive",
        [StorageProviders.Dropbox] = "dropbox",
        [StorageProviders.Backblaze] = "b2",
    };

    private static readonly string[] LoginProblemWords = ["token", "unauthor", "401", "403", "invalid_grant"];

    public StorageConfig Config => config;

    public string Remote => Rclone.RemoteName(config.Name);

    private bool UsesBrowserSignIn => config.Provider != StorageProviders.Backblaze;

    public static string SecretKey(string storageName, string option) => $"storage:{storageName}:{option}";

    public string Target(string subfolder = "", string fileName = "") =>
        $"{Remote}:{string.Join('/', new[] { config.Folder, subfolder, fileName }.Select(part => part.Trim('/')).Where(part => part.Length > 0))}";

    /// <summary>
    /// Backblaze B2 is defined entirely by environment variables, so its key never touches a file.
    /// </summary>
    public Dictionary<string, string> RemoteEnvironment()
    {
        var environment = new Dictionary<string, string>();
        if (config.Provider == StorageProviders.Backblaze)
        {
            string prefix = $"RCLONE_CONFIG_{Remote.ToUpperInvariant()}_";
            environment[prefix + "TYPE"] = "b2";
            environment[prefix + "ACCOUNT"] = config.Options.GetValueOrDefault("account", string.Empty);
            if (rclone.Credentials.Get(SecretKey(config.Name, "key")) is { } key)
            {
                environment[prefix + "KEY"] = key;
            }
        }

        return environment;
    }

    public async Task<ToolResult<Done>> SignInAsync(Func<string, string> askSecret, CancellationToken cancellationToken)
    {
        if (!rclone.IsAvailable)
        {
            return Rclone.Missing();
        }

        if (!UsesBrowserSignIn)
        {
            string key = askSecret("Backblaze application key");
            if (key.Length == 0)
            {
                return new ToolError(Rclone.Tool, "No key entered.", "Create an application key in the Backblaze console.");
            }

            rclone.Credentials.Set(SecretKey(config.Name, "key"), key);
            return ToolResult.Success();
        }

        var arguments = new List<string> { "config", "create", Remote, RcloneTypes[config.Provider] };
        if (config.Provider == StorageProviders.GoogleDrive)
        {
            arguments.Add("scope=drive");
        }

        // Interactive: rclone opens the browser for the sign-in and talks to the user directly.
        var result = await rclone.RunAsync(arguments, cancellationToken, interactive: true);
        if (!result.Succeeded)
        {
            return new ToolError(Rclone.Tool, $"Signing in to {config.Provider} did not finish.", "Run the command again and approve access in the browser.");
        }

        await rclone.EnsureEncryptedAsync(cancellationToken);
        return ToolResult.Success();
    }

    public async Task<ToolResult<Done>> TestAsync(CancellationToken cancellationToken)
    {
        if (!rclone.IsAvailable)
        {
            return Rclone.Missing();
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
                return new ToolError(
                    Rclone.Tool,
                    $"Storage \"{config.Name}\" did not work: {result.ErrorTail(3)}",
                    "The login may have expired or been revoked. Remove it with `outta storage remove NAME` and add it again.");
            }
        }

        return ToolResult.Success();
    }

    public async Task<ToolResult<string>> UploadAsync(
        string localPath, string subfolder, Action<double, double?> onProgressBytes, CancellationToken cancellationToken)
    {
        if (!rclone.IsAvailable)
        {
            return Rclone.Missing();
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
            return new ToolError(
                Rclone.Tool,
                $"Upload to {config.Name} {(loginProblem ? "was refused" : "failed")}: {message}",
                loginProblem
                    ? "The login may have expired. Remove the storage and add it again."
                    : "Check your connection and run `outta storage test NAME`.");
        }

        onProgressBytes(total, total);
        return ToolResult<string>.Success(target);
    }

    public async Task<ToolResult<Done>> ForgetAsync(CancellationToken cancellationToken)
    {
        if (UsesBrowserSignIn && rclone.IsAvailable)
        {
            await rclone.RunAsync(["config", "delete", Remote], cancellationToken);
        }

        rclone.Credentials.Delete(SecretKey(config.Name, "key"));
        return ToolResult.Success();
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
