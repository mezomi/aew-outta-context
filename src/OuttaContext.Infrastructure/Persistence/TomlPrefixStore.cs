using OuttaContext.Core.Common;
using OuttaContext.Core.Paths;
using OuttaContext.Core.Scraping;
using Tomlyn.Model;

namespace OuttaContext.Infrastructure.Persistence;

/// <summary>prefixes.toml: <c>[prefixes] dailymotion = ["https://..."]</c>.</summary>
public sealed class TomlPrefixStore(AppPaths paths) : IPrefixStore
{
    private string Location => paths.PrefixesFile;

    public IReadOnlyDictionary<string, IReadOnlyList<LinkPrefix>> All()
    {
        TomlTable prefixes = TomlFile.Table(TomlFile.ReadOrThrow(Location), "prefixes");
        return prefixes.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlyList<LinkPrefix>)(entry.Value is TomlArray items
                ? items.Select(item => item?.ToString() ?? string.Empty).Where(item => item.Length > 0).Select(LinkPrefix.FromStored).ToList()
                : []),
            StringComparer.Ordinal);
    }

    public IReadOnlyList<LinkPrefix> ForSite(string site) => All().GetValueOrDefault(site) ?? [];

    public UnitResult<Error> Add(string site, LinkPrefix prefix)
    {
        Dictionary<string, List<LinkPrefix>> data = Editable();
        List<LinkPrefix> items = data.TryGetValue(site, out List<LinkPrefix>? existing) ? existing : data[site] = [];
        if (items.Contains(prefix))
        {
            return ScrapeErrors.PrefixExists(prefix.Value, site);
        }

        items.Add(prefix);
        Save(data);
        return UnitResult.Success<Error>();
    }

    public UnitResult<Error> Remove(string site, LinkPrefix prefix)
    {
        Dictionary<string, List<LinkPrefix>> data = Editable();
        if (!data.TryGetValue(site, out List<LinkPrefix>? items) || !items.Remove(prefix))
        {
            return ScrapeErrors.PrefixNotFound(prefix.Value, site);
        }

        Save(data);
        return UnitResult.Success<Error>();
    }

    private Dictionary<string, List<LinkPrefix>> Editable() =>
        All().ToDictionary(entry => entry.Key, entry => entry.Value.ToList(), StringComparer.Ordinal);

    private void Save(Dictionary<string, List<LinkPrefix>> data)
    {
        var prefixes = new TomlTable();
        foreach ((string site, List<LinkPrefix> items) in data.Where(entry => entry.Value.Count > 0).OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            var array = new TomlArray();
            foreach (LinkPrefix item in items)
            {
                array.Add(item.Value);
            }

            prefixes[site] = array;
        }

        TomlFile.Write(Location, new TomlTable { ["prefixes"] = prefixes });
    }
}
