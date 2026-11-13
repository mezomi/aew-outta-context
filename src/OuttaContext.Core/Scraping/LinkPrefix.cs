using OuttaContext.Core.Common;

namespace OuttaContext.Core.Scraping;

/// <summary>The start of a link that counts as a video, e.g. <c>https://geo.dailymotion.com/player/</c>.</summary>
public sealed class LinkPrefix : SimpleValueObject<string>
{
    private LinkPrefix(string value)
        : base(value)
    {
    }

    public static Result<LinkPrefix, Error> Create(string text)
    {
        string trimmed = text.Trim();
        bool isLink = trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("//", StringComparison.Ordinal);
        return isLink && trimmed.Length > "https://".Length - 1 ? new LinkPrefix(trimmed) : ScrapeErrors.NotAPrefix(text);
    }

    /// <summary>For prefixes read back from prefixes.toml.</summary>
    public static LinkPrefix FromStored(string text) => new(text.Trim());

    public override string ToString() => Value;
}
