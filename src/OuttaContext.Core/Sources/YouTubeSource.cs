using System.Text.RegularExpressions;

namespace OuttaContext.Core.Sources;

public sealed partial class YouTubeSource : VideoSource
{
    public override string Name => "youtube";

    public override string Label => "YouTube";

    protected override IReadOnlyList<Regex> UrlRegexes => [WatchUrlRegex(), PathUrlRegex(), ShortUrlRegex()];

    public override string UrlForId(string videoId) => $"https://www.youtube.com/watch?v={videoId}";

    [GeneratedRegex(@"^(?:https?:)?//(?:www\.|m\.|music\.)?youtube\.com/watch\?(?:.*&)?v=([\w-]{6,})", RegexOptions.IgnoreCase)]
    private static partial Regex WatchUrlRegex();

    [GeneratedRegex(
        @"^(?:https?:)?//(?:www\.|m\.|music\.)?youtube(?:-nocookie)?\.com/(?:embed|shorts|live|v)/(?!videoseries)([\w-]{6,})",
        RegexOptions.IgnoreCase)]
    private static partial Regex PathUrlRegex();

    [GeneratedRegex(@"^(?:https?:)?//(?:www\.)?youtu\.be/([\w-]{6,})", RegexOptions.IgnoreCase)]
    private static partial Regex ShortUrlRegex();
}
