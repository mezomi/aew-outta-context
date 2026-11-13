using OuttaContext.Core.Common;

namespace OuttaContext.Core.Scraping;

/// <summary>Link prefixes the user registered per site (prefixes.toml).</summary>
public interface IPrefixStore
{
    IReadOnlyDictionary<string, IReadOnlyList<LinkPrefix>> All();

    IReadOnlyList<LinkPrefix> ForSite(string site);

    /// <returns>A failure if the prefix is already registered.</returns>
    UnitResult<Error> Add(string site, LinkPrefix prefix);

    /// <returns>A failure if the prefix isn't registered.</returns>
    UnitResult<Error> Remove(string site, LinkPrefix prefix);
}
