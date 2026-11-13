using OuttaContext.Core.Common;

namespace OuttaContext.Core.Settings;

/// <summary>Loads and saves <see cref="OuttaSettings"/> (settings.toml).</summary>
public interface ISettingsStore
{
    string Location { get; }

    /// <summary>The saved settings, or the defaults when there is no file yet.</summary>
    /// <returns>A failure when the file exists but can't be read.</returns>
    Result<OuttaSettings, Error> Load();

    void Save(OuttaSettings settings);
}
