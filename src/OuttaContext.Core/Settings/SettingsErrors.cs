using OuttaContext.Core.Common;

namespace OuttaContext.Core.Settings;

/// <summary>Errors for `outta config set` and the settings file.</summary>
public static class SettingsErrors
{
    public static Error UnknownKey(string key) =>
        new("settings.unknown_key", $"Unknown setting \"{key}\".", "Settings you can change: " + string.Join(", ", SettingKeys.Descriptions.Keys));

    public static Error NotALength(string value, string example) =>
        new("settings.not_a_length", $"\"{value}\" is not a length.", $"Use seconds or M:SS, e.g. {example}.");

    public static Error EpisodeTooShort() =>
        new("settings.episode_too_short", "Episode length must be at least 10 seconds.", "e.g. `outta config set episode-length 10:00`");

    public static Error UnsupportedQuality(string value) =>
        new("settings.quality", $"\"{value}\" is not a supported quality.", "Use one of: " + string.Join(", ", VideoQuality.All));

    public static Error UnsupportedBrowser(string value) =>
        new("settings.browser", $"\"{value}\" is not a supported browser.", "Use one of: " + string.Join(", ", BrowserName.Supported));

    public static Error UnreadableFile(string path, string problem) =>
        new("settings.file", $"Can't read {path}: {problem}", "Fix or delete the file; `outta config show` prints where it is.");

    public static Error EmptyFolder() =>
        new("settings.folder_empty", "The folder is empty.", "e.g. `outta config set-destination \"C:\\Clips\"`");
}
