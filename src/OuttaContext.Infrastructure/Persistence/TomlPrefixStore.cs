using OuttaContext.Core.Errors;
using OuttaContext.Core.Scraping;
using Tomlyn.Model;

namespace OuttaContext.Infrastructure.Persistence;

/// <summary>prefixes.toml: <c>[prefixes] dailymotion = ["https://..."]</c>.</summary>
public sealed class TomlPrefixStore(string path) : IPrefixStore
{
    private const string ListHint = "See registered prefixes with `outta scrape prefixes list`.";

    public IReadOnlyDictionary<string, IReadOnlyList<string>> All()
    {
        TomlTable prefixes = TomlFile.Table(TomlFile.Read(path), "prefixes");
        return prefixes.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlyList<string>)(entry.Value is TomlArray items ? items.Select(item => item?.ToString() ?? string.Empty).ToList() : []),
            StringComparer.Ordinal);
    }

    public IReadOnlyList<string> ForSite(string site) => All().GetValueOrDefault(site) ?? [];

    public void Add(string site, string prefix)
    {
        string trimmed = prefix.Trim();
        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            throw new SettingsException(
                $"\"{trimmed}\" is not a link prefix.", "Prefixes are the start of a link, e.g. \"https://geo.dailymotion.com/player/\".");
        }

        Dictionary<string, List<string>> data = Editable();
        List<string> items = data.TryGetValue(site, out List<string>? existing) ? existing : data[site] = [];
        if (items.Contains(trimmed))
        {
            throw new AlreadyExistsException($"\"{trimmed}\" is already registered for {site}.", ListHint);
        }

        items.Add(trimmed);
        Save(data);
    }

    public void Remove(string site, string prefix)
    {
        Dictionary<string, List<string>> data = Editable();
        if (!data.TryGetValue(site, out List<string>? items) || !items.Remove(prefix.Trim()))
        {
            throw new NotFoundException($"\"{prefix}\" is not registered for {site}.", ListHint);
        }

        Save(data);
    }

    private Dictionary<string, List<string>> Editable() =>
        All().ToDictionary(entry => entry.Key, entry => entry.Value.ToList(), StringComparer.Ordinal);

    private void Save(Dictionary<string, List<string>> data)
    {
        var prefixes = new TomlTable();
        foreach ((string site, List<string> items) in data.Where(entry => entry.Value.Count > 0).OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            var array = new TomlArray();
            foreach (string item in items)
            {
                array.Add(item);
            }

            prefixes[site] = array;
        }

        TomlFile.Write(path, new TomlTable { ["prefixes"] = prefixes });
    }
}
