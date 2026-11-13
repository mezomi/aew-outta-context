using OuttaContext.Core.Common;
using OuttaContext.Core.Storage;
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

    public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        [Destination] = "Folder where clips are saved locally",
        [EpisodeLength] = "Target episode length, e.g. 10:00",
        [MaxClipLength] = "Longest clip `outta clip` accepts (catches typos); `off` for no cap",
        [MaxQuality] = "Highest video height to download: 360, 480, 720, 1080, 1440 or 2160",
        [DefaultStorage] = "Storage name used by --upload and `outta upload`",
        [CookiesFromBrowser] = "Browser whose login cookies yt-dlp uses: firefox, edge, chrome or off",
    };

    private static readonly VideoTime ShortestEpisode = VideoTime.FromTrusted(10);

    private static readonly string[] OffWords = ["", "off", "none", "0"];

    /// <summary>Returns <paramref name="settings"/> with <paramref name="key"/> set to <paramref name="value"/>.</summary>
    public static Result<OuttaSettings, Error> Apply(OuttaSettings settings, string key, string value)
    {
        string normalizedKey = key.Trim().ToLowerInvariant().Replace('_', '-');
        string trimmed = value.Trim();
        return normalizedKey switch
        {
            Destination or "clip-folder" => ParseFolder(trimmed).Map(folder => settings with { ClipFolder = folder }),
            EpisodeLength => ParseEpisodeLength(trimmed).Map(length => settings with { EpisodeLength = length }),
            MaxClipLength or "clip-cap" => ParseClipCap(trimmed).Map((Maybe<VideoTime> cap) => settings with { MaxClipLength = cap }),
            MaxQuality => VideoQuality.Parse(trimmed).Map(quality => settings with { MaxQuality = quality }),
            DefaultStorage => ParseOptional(trimmed, StorageName.Create).Map((Maybe<StorageName> name) => settings with { DefaultStorage = name }),
            CookiesFromBrowser => ParseOptional(trimmed, BrowserName.Parse).Map((Maybe<BrowserName> browser) => settings with { CookiesFromBrowser = browser }),
            _ => SettingsErrors.UnknownKey(normalizedKey),
        };
    }

    /// <summary>How each setting is shown by `outta config show`.</summary>
    public static IReadOnlyDictionary<string, string> Display(OuttaSettings settings) =>
        new Dictionary<string, string>
        {
            [Destination] = settings.ClipFolder,
            [EpisodeLength] = settings.EpisodeLength.ToClock(),
            [MaxClipLength] = settings.MaxClipLength.Map(cap => cap.ToShortLength()).GetValueOrDefault("off"),
            [MaxQuality] = settings.MaxQuality.ToString(),
            [DefaultStorage] = settings.DefaultStorage.Map(name => name.Value).GetValueOrDefault("(none)"),
            [CookiesFromBrowser] = settings.CookiesFromBrowser.Map(browser => browser.Value).GetValueOrDefault("off"),
        };

    private static Result<string, Error> ParseFolder(string path)
    {
        if (path.Length == 0)
        {
            return SettingsErrors.EmptyFolder();
        }

        return path.StartsWith('~')
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + path[1..]
            : path;
    }

    private static Result<VideoTime, Error> ParseEpisodeLength(string value) =>
        ParseLength(value, "`outta config set episode-length 10:00`")
            .Ensure(length => length.IsAtLeast(ShortestEpisode), SettingsErrors.EpisodeTooShort());

    private static Result<Maybe<VideoTime>, Error> ParseClipCap(string value) =>
        IsOff(value)
            ? Maybe<VideoTime>.None
            : ParseLength(value, "`outta config set max-clip-length 45` (or `off`)").Map(Maybe.From);

    private static Result<VideoTime, Error> ParseLength(string value, string example) =>
        VideoTime.Parse(value).MapError(_ => SettingsErrors.NotALength(value, example));

    /// <summary>"off" (or empty) clears the setting; anything else must parse.</summary>
    private static Result<Maybe<T>, Error> ParseOptional<T>(string value, Func<string, Result<T, Error>> parse) =>
        IsOff(value) ? Maybe<T>.None : parse(value).Map(Maybe.From);

    private static bool IsOff(string value) => OffWords.Contains(value.ToLowerInvariant());
}
