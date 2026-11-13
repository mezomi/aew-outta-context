using OuttaContext.Core.Errors;

namespace OuttaContext.Core.Sources;

/// <summary>Picks the <see cref="VideoSource"/> for a URL. Order matters: specific sites first.</summary>
public sealed class SourceRegistry(IEnumerable<VideoSource> sources)
{
    private readonly IReadOnlyList<VideoSource> _sources = [.. sources];

    public SourceRegistry()
        : this([new DailymotionSource(), new YouTubeSource(), new DirectStreamSource()])
    {
    }

    public VideoSource Detect(string url) =>
        _sources.FirstOrDefault(source => source.Matches(url))
        ?? throw new UnsupportedUrlException($"Not a Dailymotion or YouTube video link (or a direct .m3u8/.mp4 link): {url}");

    public VideoSource Get(string name) =>
        _sources.FirstOrDefault(source => source.Name == name)
        ?? throw new KeyNotFoundException($"No source named {name}.");
}
