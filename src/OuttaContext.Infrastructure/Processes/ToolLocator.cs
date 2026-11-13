using OuttaContext.Core.Paths;

namespace OuttaContext.Infrastructure.Processes;

/// <summary>
/// Finds outside programs: first in the app's own tools folder (where `outta update` puts
/// yt-dlp), then on PATH. Adds ".exe" on Windows.
/// </summary>
public sealed class ToolLocator(AppPaths paths, Func<string, string?>? environmentVariable = null)
{
    private readonly Func<string, string?> _environmentVariable = environmentVariable ?? Environment.GetEnvironmentVariable;

    public static string ExecutableName(string name) => OperatingSystem.IsWindows() ? name + ".exe" : name;

    /// <summary>Where the app keeps its own copy of a tool (it may not exist yet).</summary>
    public string ManagedPath(string name) => Path.Combine(paths.ToolsDirectory, ExecutableName(name));

    public string? Find(string name)
    {
        string managed = ManagedPath(name);
        if (File.Exists(managed))
        {
            return managed;
        }

        string pathVariable = _environmentVariable("PATH") ?? string.Empty;
        foreach (string directory in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(directory.Trim('"'), ExecutableName(name));
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
