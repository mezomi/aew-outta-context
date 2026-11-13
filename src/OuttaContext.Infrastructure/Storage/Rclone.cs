using System.Security.Cryptography;
using System.Text.RegularExpressions;
using OuttaContext.Core.Results;
using OuttaContext.Core.Storage;
using OuttaContext.Infrastructure.Processes;

namespace OuttaContext.Infrastructure.Storage;

/// <summary>
/// Runs rclone with the app's own config file (rclone.conf in the data folder). That file is
/// encrypted with a random password kept in the keychain; the password reaches rclone only through
/// an environment variable, never a file or a command-line argument.
/// </summary>
public sealed partial class Rclone(IProcessRunner runner, ToolLocator tools, ICredentialStore credentials, string configPath)
{
    public const string Tool = "rclone";
    public const string ConfigPasswordKey = "rclone-config-pass";

    public ICredentialStore Credentials => credentials;

    public bool IsAvailable => tools.Find("rclone") is not null;

    public static string RemoteName(string storageName) => "outta_" + UnsafeRemoteCharsRegex().Replace(storageName.ToLowerInvariant(), "_");

    public static ToolError Missing() =>
        new(Tool, "rclone was not found.", "Install it: `winget install Rclone.Rclone`, then open a new terminal.");

    /// <summary>Environment for every rclone run: the config password (if any) plus <paramref name="extra"/>.</summary>
    public Dictionary<string, string> Environment(IReadOnlyDictionary<string, string>? extra = null)
    {
        var environment = new Dictionary<string, string> { ["RCLONE_ASK_PASSWORD"] = "false" };
        if (SafeGet(ConfigPasswordKey) is { } password)
        {
            environment["RCLONE_CONFIG_PASS"] = password;
        }

        foreach ((string name, string value) in extra ?? new Dictionary<string, string>())
        {
            environment[name] = value;
        }

        return environment;
    }

    public IReadOnlyList<string> Command(params string[] arguments) => ["--config", configPath, .. arguments];

    public async Task<ProcessResult> RunAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? extraEnvironment = null,
        string? input = null,
        bool interactive = false,
        Action<string>? onErrorLine = null)
    {
        string rclone = tools.Find("rclone") ?? "rclone";
        return await runner.RunAsync(
            new ProcessSpec(rclone, Command([.. arguments]))
            {
                Environment = Environment(extraEnvironment),
                StandardInput = input,
                Interactive = interactive,
                OnErrorLine = onErrorLine,
            },
            cancellationToken);
    }

    /// <summary>Encrypts rclone.conf with a random password saved in the keychain (once).</summary>
    public async Task<bool> EnsureEncryptedAsync(CancellationToken cancellationToken)
    {
        if (SafeGet(ConfigPasswordKey) is not null)
        {
            return true;
        }

        if (!credentials.Describe().Available)
        {
            return false;
        }

        string password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(configPath))!);
        if (!File.Exists(configPath))
        {
            await File.WriteAllTextAsync(configPath, string.Empty, cancellationToken);
        }

        // rclone reads the new password twice from standard input when it is not a terminal.
        ProcessResult result = await runner.RunAsync(
            new ProcessSpec(tools.Find("rclone") ?? "rclone", Command("config", "encryption", "set"))
            {
                Environment = new Dictionary<string, string> { ["RCLONE_ASK_PASSWORD"] = "true" },
                StandardInput = $"{password}\n{password}\n",
            },
            cancellationToken);
        if (!result.Succeeded)
        {
            return false;
        }

        credentials.Set(ConfigPasswordKey, password);
        return true;
    }

    private string? SafeGet(string key)
    {
        try
        {
            return credentials.Get(key);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    [GeneratedRegex("[^a-z0-9]")]
    private static partial Regex UnsafeRemoteCharsRegex();
}
