using System.Collections.Immutable;

namespace OuttaContext.Core.Sources;

/// <summary>Extra HTTP headers sent when asking for a video (e.g. Referer). Compared by content.</summary>
public sealed class RequestHeaders : ValueObject
{
    public static readonly RequestHeaders None = new(ImmutableSortedDictionary<string, string>.Empty);

    private RequestHeaders(ImmutableSortedDictionary<string, string> values)
    {
        Values = values;
    }

    public IReadOnlyDictionary<string, string> Values { get; }

    public bool IsEmpty => Values.Count == 0;

    public static RequestHeaders From(IEnumerable<KeyValuePair<string, string>> headers) =>
        new(headers.ToImmutableSortedDictionary(StringComparer.OrdinalIgnoreCase));

    /// <summary>A Referer header for the page the video was found on, or no headers.</summary>
    public static RequestHeaders RefererFor(Maybe<string> pageUrl) =>
        pageUrl.TryGetValue(out string? page) ? From([new("Referer", page)]) : None;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        foreach (KeyValuePair<string, string> header in Values)
        {
            yield return header.Key.ToUpperInvariant();
            yield return header.Value;
        }
    }
}
