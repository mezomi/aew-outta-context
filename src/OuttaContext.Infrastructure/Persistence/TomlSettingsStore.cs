using System.Globalization;
using OuttaContext.Core.Common;
using OuttaContext.Core.Paths;
using OuttaContext.Core.Settings;
using OuttaContext.Core.Storage;
using OuttaContext.Core.Time;
using Tomlyn.Model;

namespace OuttaContext.Infrastructure.Persistence;

/// <summary>settings.toml. Keys are snake_case (clip_folder, episode_length, ...), as in the Python version.</summary>
public sealed class TomlSettingsStore(AppPaths paths) : ISettingsStore
{
    public string Location => paths.SettingsFile;

    public Result<OuttaSettings, Error> Load() =>
        TomlFile.Read(Location).Bind(table =>
        {
            try
            {
                return Parse(table);
            }
            catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
            {
                return Result.Failure<OuttaSettings, Error>(SettingsErrors.UnreadableFile(Location, $"a bad value ({exception.Message})"));
            }
        });

    public void Save(OuttaSettings settings)
    {
        var table = new TomlTable
        {
            ["clip_folder"] = settings.ClipFolder,
            ["episode_length"] = settings.EpisodeLength.Seconds,
            ["max_quality"] = (long)settings.MaxQuality.Height,
            ["max_clip_length"] = settings.MaxClipLength.Map(cap => cap.Seconds).GetValueOrDefault(0),
        };
        settings.DefaultStorage.Tap(storage => table["default_storage"] = storage.Value);
        settings.CookiesFromBrowser.Tap(browser => table["cookies_from_browser"] = browser.Value);
        TomlFile.Write(Location, table);
    }

    private Result<OuttaSettings, Error> Parse(TomlTable table)
    {
        var settings = new OuttaSettings();
        if (TomlFile.Text(table, "clip_folder").TryGetValue(out string? folder))
        {
            settings = settings with { ClipFolder = folder };
        }

        if (table.TryGetValue("episode_length", out object? length))
        {
            Result<VideoTime, Error> episodeLength = Seconds(length, "episode_length");
            if (episodeLength.IsFailure)
            {
                return episodeLength.Error;
            }

            settings = settings with { EpisodeLength = episodeLength.Value };
        }

        if (table.TryGetValue("max_quality", out object? quality))
        {
            Result<VideoQuality, Error> maxQuality = VideoQuality.FromHeight(Convert.ToInt32(quality, CultureInfo.InvariantCulture));
            if (maxQuality.IsFailure)
            {
                return SettingsErrors.UnreadableFile(Location, $"max_quality: {maxQuality.Error.Message}");
            }

            settings = settings with { MaxQuality = maxQuality.Value };
        }

        if (table.TryGetValue("max_clip_length", out object? cap))
        {
            // 0 means "no cap" (the Python version's format).
            double seconds = Convert.ToDouble(cap, CultureInfo.InvariantCulture);
            settings = settings with { MaxClipLength = seconds > 0 ? VideoTime.FromSeconds(seconds).ValueOrNone() : Maybe<VideoTime>.None };
        }

        if (TomlFile.Text(table, "default_storage").TryGetValue(out string? storage))
        {
            settings = settings with { DefaultStorage = StorageName.Create(storage).ValueOrNone() };
        }

        if (TomlFile.Text(table, "cookies_from_browser").TryGetValue(out string? browser))
        {
            Result<BrowserName, Error> cookies = BrowserName.Parse(browser);
            if (cookies.IsFailure)
            {
                return SettingsErrors.UnreadableFile(Location, $"cookies_from_browser: {cookies.Error.Message}");
            }

            settings = settings with { CookiesFromBrowser = cookies.Value };
        }

        return settings;
    }

    private Result<VideoTime, Error> Seconds(object value, string key) =>
        VideoTime.FromSeconds(Convert.ToDouble(value, CultureInfo.InvariantCulture))
            .MapError(error => SettingsErrors.UnreadableFile(Location, $"{key}: {error.Message}"));
}
