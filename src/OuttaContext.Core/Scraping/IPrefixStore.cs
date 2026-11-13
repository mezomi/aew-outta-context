namespace OuttaContext.Core.Scraping;

/// <summary>Link prefixes the user registered per site (prefixes.toml).</summary>
public interface IPrefixStore
{
    IReadOnlyDictionary<string, IReadOnlyList<string>> All();

    IReadOnlyList<string> ForSite(string site);

    void Add(string site, string prefix);

    void Remove(string site, string prefix);
}
