using OuttaContext.Core.Storage;
using OuttaContext.Core.Time;

namespace OuttaContext.Core.Settings;

/// <summary>The user's settings (settings.toml). Defaults apply when a key is missing.</summary>
public sealed record OuttaSettings
{
    public static readonly VideoTime DefaultEpisodeLength = VideoTime.FromTrusted(600);
    public static readonly VideoTime DefaultMaxClipLength = VideoTime.FromTrusted(45);

    /// <summary>Where clips are saved. Built episodes go in an "Episodes" subfolder.</summary>
    public string ClipFolder { get; init; } = DefaultClipFolder();

    /// <summary>Target episode length (10:00).</summary>
    public VideoTime EpisodeLength { get; init; } = DefaultEpisodeLength;

    /// <summary>Highest video height to download.</summary>
    public VideoQuality MaxQuality { get; init; } = VideoQuality.Default;

    /// <summary>Longest clip `outta clip` accepts, to catch typos. None means no cap.</summary>
    public Maybe<VideoTime> MaxClipLength { get; init; } = DefaultMaxClipLength;

    /// <summary>Storage used by --upload when none is named.</summary>
    public Maybe<StorageName> DefaultStorage { get; init; }

    /// <summary>Browser whose cookies yt-dlp uses.</summary>
    public Maybe<BrowserName> CookiesFromBrowser { get; init; }

    public string EpisodesFolder => Path.Combine(ClipFolder, "Episodes");

    public static string DefaultClipFolder() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify),
            "Videos",
            "Outta Context");
}
