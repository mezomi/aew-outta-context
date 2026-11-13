namespace OuttaContext.Core.Settings;

/// <summary>Loads and saves <see cref="OuttaSettings"/> (settings.toml).</summary>
public interface ISettingsStore
{
    string Location { get; }

    OuttaSettings Load();

    void Save(OuttaSettings settings);

    /// <summary>Applies `outta config set KEY VALUE`, saves, and returns the new settings.</summary>
    OuttaSettings Set(string key, string value);
}
