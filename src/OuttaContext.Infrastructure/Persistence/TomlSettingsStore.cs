using System.Globalization;
using OuttaContext.Core.Errors;
using OuttaContext.Core.Settings;
using Tomlyn.Model;

namespace OuttaContext.Infrastructure.Persistence;

/// <summary>settings.toml. Keys are snake_case (clip_folder, episode_length, ...).</summary>
public sealed class TomlSettingsStore(string path) : ISettingsStore
{
    public string Location => path;

    public OuttaSettings Load()
    {
        TomlTable table = TomlFile.Read(path);
        var settings = new OuttaSettings();
        try
        {
            if (table.TryGetValue("clip_folder", out object? folder))
            {
                settings = settings with { ClipFolder = Convert.ToString(folder, CultureInfo.InvariantCulture)! };
            }

            if (table.TryGetValue("episode_length", out object? length))
            {
                settings = settings with { EpisodeLength = Convert.ToDouble(length, CultureInfo.InvariantCulture) };
            }

            if (table.TryGetValue("max_quality", out object? quality))
            {
                settings = settings with { MaxQuality = Convert.ToInt32(quality, CultureInfo.InvariantCulture) };
            }

            if (table.TryGetValue("max_clip_length", out object? cap))
            {
                double seconds = Convert.ToDouble(cap, CultureInfo.InvariantCulture);
                settings = settings with { MaxClipLength = seconds > 0 ? seconds : null };
            }

            if (table.TryGetValue("default_storage", out object? storage) && storage is string { Length: > 0 } name)
            {
                settings = settings with { DefaultStorage = name };
            }

            if (table.TryGetValue("cookies_from_browser", out object? browser) && browser is string { Length: > 0 } cookies)
            {
                settings = settings with { CookiesFromBrowser = cookies };
            }
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            throw new SettingsException($"Settings file {path} has a bad value: {exception.Message}");
        }

        return settings;
    }

    public void Save(OuttaSettings settings)
    {
        var table = new TomlTable
        {
            ["clip_folder"] = settings.ClipFolder,
            ["episode_length"] = settings.EpisodeLength,
            ["max_quality"] = (long)settings.MaxQuality,
            ["max_clip_length"] = settings.MaxClipLength ?? 0,
        };
        if (settings.DefaultStorage is { } storage)
        {
            table["default_storage"] = storage;
        }

        if (settings.CookiesFromBrowser is { } browser)
        {
            table["cookies_from_browser"] = browser;
        }

        TomlFile.Write(path, table);
    }

    public OuttaSettings Set(string key, string value)
    {
        OuttaSettings updated = SettingKeys.Apply(Load(), key, value);
        Save(updated);
        return updated;
    }
}
