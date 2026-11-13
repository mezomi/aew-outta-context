using System.Globalization;
using OuttaContext.Core.Errors;
using OuttaContext.Core.Time;

namespace OuttaContext.Core.Settings;

/// <summary>The keys `outta config set KEY VALUE` accepts, and how each value is read.</summary>
public static class SettingKeys
{
    public const string Destination = "destination";
    public const string EpisodeLength = "episode-length";
    public const string MaxClipLength = "max-clip-length";
    public const string MaxQuality = "max-quality";
    public const string DefaultStorage = "default-storage";
    public const string CookiesFromBrowser = "cookies-from-browser";

    public static readonly IReadOnlyList<int> Qualities = [360, 480, 720, 1080, 1440, 2160];

    public static readonly IReadOnlyList<string> Browsers =
        ["firefox", "edge", "chrome", "brave", "opera", "vivaldi", "chromium", "safari"];

    public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        [Destination] = "Folder where clips are saved locally",
        [EpisodeLength] = "Target episode length, e.g. 10:00",
        [MaxClipLength] = "Longest clip `outta clip` accepts (catches typos); `off` for no cap",
        [MaxQuality] = "Highest video height to download: 360, 480, 720, 1080, 1440 or 2160",
        [DefaultStorage] = "Storage name used by --upload and `outta upload`",
        [CookiesFromBrowser] = "Browser whose login cookies yt-dlp uses: firefox, edge, chrome or off",
    };

    private static readonly string[] OffWords = ["", "off", "none", "0"];

    /// <summary>Returns <paramref name="settings"/> with <paramref name="key"/> set to <paramref name="value"/>.</summary>
    public static OuttaSettings Apply(OuttaSettings settings, string key, string value)
    {
        string normalizedKey = key.Trim().ToLowerInvariant().Replace('_', '-');
        string trimmed = value.Trim();
        return normalizedKey switch
        {
            Destination or "clip-folder" => settings with { ClipFolder = ExpandHome(trimmed) },
            EpisodeLength => settings with { EpisodeLength = ParseEpisodeLength(trimmed) },
            MaxClipLength or "clip-cap" => settings with { MaxClipLength = ParseClipCap(trimmed) },
            MaxQuality => settings with { MaxQuality = ParseQuality(trimmed) },
            DefaultStorage => settings with { DefaultStorage = trimmed.Length == 0 ? null : trimmed },
            CookiesFromBrowser => settings with { CookiesFromBrowser = ParseBrowser(trimmed) },
            _ => throw new SettingsException(
                $"Unknown setting \"{normalizedKey}\".",
                "Settings you can change: " + string.Join(", ", Descriptions.Keys)),
        };
    }

    /// <summary>How each setting is shown by `outta config show`.</summary>
    public static IReadOnlyDictionary<string, string> Display(OuttaSettings settings) =>
        new Dictionary<string, string>
        {
            [Destination] = settings.ClipFolder,
            [EpisodeLength] = TimeText.Format(settings.EpisodeLength),
            [MaxClipLength] = settings.MaxClipLength is { } cap ? TimeText.FormatLength(cap) : "off",
            [MaxQuality] = string.Create(CultureInfo.InvariantCulture, $"{settings.MaxQuality}p"),
            [DefaultStorage] = settings.DefaultStorage ?? "(none)",
            [CookiesFromBrowser] = settings.CookiesFromBrowser ?? "off",
        };

    private static string ExpandHome(string path) =>
        path.StartsWith('~')
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + path[1..]
            : path;

    private static double ParseEpisodeLength(string value)
    {
        double seconds = ParseLength(value, "`outta config set episode-length 10:00`");
        return seconds < 10
            ? throw new SettingsException("Episode length must be at least 10 seconds.", "e.g. 10:00")
            : seconds;
    }

    private static double? ParseClipCap(string value) =>
        OffWords.Contains(value.ToLowerInvariant())
            ? null
            : ParseLength(value, "`outta config set max-clip-length 45` (or `off`)");

    private static double ParseLength(string value, string example)
    {
        try
        {
            return TimeText.ParseSeconds(value);
        }
        catch (InvalidTimeRangeException)
        {
            throw new SettingsException($"\"{value}\" is not a length.", $"Use seconds or M:SS, e.g. {example}.");
        }
    }

    private static int ParseQuality(string value)
    {
        string digits = value.ToLowerInvariant().TrimEnd('p');
        return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out int quality)
            && Qualities.Contains(quality)
            ? quality
            : throw new SettingsException(
                $"\"{value}\" is not a supported quality.", "Use one of: " + string.Join(", ", Qualities));
    }

    private static string? ParseBrowser(string value)
    {
        string lowered = value.ToLowerInvariant();
        if (OffWords.Contains(lowered))
        {
            return null;
        }

        return Browsers.Contains(lowered.Split(':')[0])
            ? lowered
            : throw new SettingsException(
                $"\"{value}\" is not a supported browser.", "Use one of: " + string.Join(", ", Browsers));
    }
}
