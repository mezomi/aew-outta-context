namespace OuttaContext.Core.Media;

/// <summary>What yt-dlp tells us about a video.</summary>
public sealed record VideoInfo(string VideoId, string Title, double? Duration, string WebpageUrl, string Extractor);

/// <summary>One stream to download: a video+audio file, or a video-only / audio-only part.</summary>
/// <param name="Protocol">yt-dlp's protocol name: "m3u8_native" for HLS, "https" for a plain file.</param>
public sealed record StreamFormat(string Url, string Protocol, IReadOnlyDictionary<string, string> Headers)
{
    public bool IsHls => Protocol is "m3u8" or "m3u8_native";
}

/// <summary>A video plus the streams chosen for it (best quality up to the user's maximum).</summary>
public sealed record ResolvedVideo(VideoInfo Info, IReadOnlyList<StreamFormat> Streams)
{
    public bool IsHls => Streams.Count > 0 && Streams.All(stream => stream.IsHls);
}

/// <summary>One input for an ffmpeg cut: a local file or URL, and where in it the clip starts.</summary>
public sealed record CutInput(string Source, double Offset, IReadOnlyDictionary<string, string>? Headers = null)
{
    public bool IsRemote =>
        Source.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || Source.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
}

/// <summary>The size, frame rate and codecs every clip is converted to before joining.</summary>
public sealed record NormalizeTarget(
    int Width = 1920,
    int Height = 1080,
    int Fps = 30,
    int AudioRate = 48000,
    string VideoCodec = "libx264",
    string AudioCodec = "aac",
    int Crf = 20,
    string Preset = "veryfast")
{
    public string CacheKey => $"{Width}x{Height}@{Fps}-{VideoCodec}-{Crf}-{AudioRate}";
}

/// <summary>What ffprobe says about a video file.</summary>
public sealed record ProbeResult(double Duration, int? Width, int? Height, double? Fps, bool HasAudio);

/// <summary>A clip written to disk.</summary>
public sealed record DownloadedClip(string Path, long SizeBytes);
