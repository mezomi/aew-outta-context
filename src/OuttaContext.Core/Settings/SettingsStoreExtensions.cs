using OuttaContext.Core.Common;

namespace OuttaContext.Core.Settings;

public static class SettingsStoreExtensions
{
    /// <summary>`outta config set KEY VALUE`: load, change one setting, save.</summary>
    /// <returns>The saved settings, or why the file or the value is not usable (nothing is saved then).</returns>
    public static Result<OuttaSettings, Error> Change(this ISettingsStore store, string key, string value) =>
        store.Load()
            .Bind(settings => SettingKeys.Apply(settings, key, value))
            .Tap(store.Save);
}
