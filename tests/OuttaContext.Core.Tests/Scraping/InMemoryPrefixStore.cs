using OuttaContext.Core.Common;
using OuttaContext.Core.Scraping;

namespace OuttaContext.Core.Tests.Scraping;

/// <summary>Prefixes kept in memory, for scraper tests.</summary>
internal sealed class InMemoryPrefixStore : IPrefixStore
{
    private readonly Dictionary<string, List<LinkPrefix>> _data = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, IReadOnlyList<LinkPrefix>> All() => _data.ToDictionary(e => e.Key, e => (IReadOnlyList<LinkPrefix>)e.Value);

    public IReadOnlyList<LinkPrefix> ForSite(string site) => _data.GetValueOrDefault(site) ?? [];

    public UnitResult<Error> Add(string site, LinkPrefix prefix)
    {
        (_data.TryGetValue(site, out var list) ? list : _data[site] = []).Add(prefix);
        return UnitResult.Success<Error>();
    }

    public UnitResult<Error> Remove(string site, LinkPrefix prefix) =>
        _data.TryGetValue(site, out var list) && list.Remove(prefix) ? UnitResult.Success<Error>() : ScrapeErrors.PrefixNotFound(prefix.Value, site);
}
