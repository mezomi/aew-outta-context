using OuttaContext.Core.Common;

namespace OuttaContext.Core.Sources;

/// <summary>Picks the <see cref="VideoSource"/> for a URL. Order matters: specific sites first.</summary>
public sealed class SourceRegistry
{
    private readonly IReadOnlyList<VideoSource> _sources;

    public SourceRegistry(IEnumerable<VideoSource> sources)
    {
        _sources = [.. sources];
    }

    /// <summary>Dailymotion, YouTube, then direct stream links.</summary>
    public static SourceRegistry Default { get; } =
        new([new DailymotionSource(), new YouTubeSource(), new DirectStreamSource()]);

    public Result<VideoLink, Error> Detect(string url) =>
        _sources
            .Select(source => source.Recognize(url))
            .FirstOrDefault(link => link.HasValue)
            .ToResult(SourceErrors.Unsupported(url));
}
