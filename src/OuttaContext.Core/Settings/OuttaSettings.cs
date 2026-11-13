namespace OuttaContext.Core.Settings;

/// <summary>The user's settings (settings.toml). Defaults apply when a key is missing.</summary>
public sealed record OuttaSettings
{
    public const double DefaultEpisodeLength = 600;
    public const int DefaultMaxQuality = 1080;
    public const double DefaultMaxClipLength = 45;

    /// <summary>Where clips are saved. Built episodes go in an "Episodes" subfolder.</summary>
    public string ClipFolder { get; init; } = DefaultClipFolder();

    /// <summary>Target episode length in seconds (10:00).</summary>
    public double EpisodeLength { get; init; } = DefaultEpisodeLength;

    /// <summary>Highest video height to download.</summary>
    public int MaxQuality { get; init; } = DefaultMaxQuality;

    /// <summary>Longest clip `outta clip` accepts, to catch typos. Null means no cap.</summary>
    public double? MaxClipLength { get; init; } = DefaultMaxClipLength;

    /// <summary>Storage used by --upload when none is named.</summary>
    public string? DefaultStorage { get; init; }

    /// <summary>Browser whose cookies yt-dlp uses (firefox, edge, chrome), or null.</summary>
    public string? CookiesFromBrowser { get; init; }

    public string EpisodesFolder => Path.Combine(ClipFolder, "Episodes");

    public static string DefaultClipFolder() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify),
            "Videos",
            "Outta Context");
}
