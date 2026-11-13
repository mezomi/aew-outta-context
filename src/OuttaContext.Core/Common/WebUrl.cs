namespace OuttaContext.Core.Common;

/// <summary>An absolute http:// or https:// address: a video page, a web page or a stream.</summary>
public sealed class WebUrl : SimpleValueObject<string>
{
    private WebUrl(Uri uri)
        : base(uri.OriginalString)
    {
        Uri = uri;
    }

    public Uri Uri { get; }

    public string Host => Uri.Host;

    public static Result<WebUrl, Error> Create(string text)
    {
        string trimmed = text.Trim();
        return Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? new WebUrl(uri)
            : new Error("url.invalid", $"\"{text}\" is not a web address.", "Links start with https://");
    }

    /// <summary>For addresses the app builds itself; a bad one is a bug, so this throws.</summary>
    public static WebUrl FromTrusted(string text) =>
        Create(text).GetValueOrDefault() ?? throw new ArgumentException($"Not a web address: {text}", nameof(text));

    /// <summary>Resolves a link that may be relative (e.g. "seg-1.ts" in a playlist) against this address.</summary>
    public WebUrl Resolve(string relativeOrAbsolute) => new(new Uri(Uri, relativeOrAbsolute));

    public override string ToString() => Value;
}
