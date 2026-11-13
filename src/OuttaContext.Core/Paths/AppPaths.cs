namespace OuttaContext.Core.Paths;

/// <summary>
/// Where the app keeps its data. On Windows: <c>%LOCALAPPDATA%\outta-context</c>.
/// Set the <c>OUTTA_HOME</c> environment variable to use another folder (tests, portable use).
/// </summary>
public sealed record AppPaths(string DataDirectory)
{
    public const string AppFolderName = "outta-context";
    public const string HomeVariable = "OUTTA_HOME";

    public string SettingsFile => Path.Combine(DataDirectory, "settings.toml");

    public string PrefixesFile => Path.Combine(DataDirectory, "prefixes.toml");

    public string LibraryDatabase => Path.Combine(DataDirectory, "library.db");

    public string StorageFile => Path.Combine(DataDirectory, "storage.toml");

    public string RcloneConfig => Path.Combine(DataDirectory, "rclone.conf");

    public string CacheDirectory => Path.Combine(DataDirectory, "cache");

    /// <summary>Tools the app downloads itself (yt-dlp).</summary>
    public string ToolsDirectory => Path.Combine(DataDirectory, "bin");

    public static AppPaths FromEnvironment()
    {
        string? overridden = Environment.GetEnvironmentVariable(HomeVariable);
        if (!string.IsNullOrWhiteSpace(overridden))
        {
            return new AppPaths(overridden);
        }

        string local = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
        return new AppPaths(Path.Combine(local, AppFolderName));
    }

    public void EnsureCreated() => Directory.CreateDirectory(DataDirectory);
}
