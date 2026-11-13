using OuttaContext.Core.Sources;

namespace OuttaContext.Core.Media;

/// <summary>One stream to download: a video+audio file, or a video-only / audio-only part.</summary>
/// <param name="Protocol">yt-dlp's protocol name: "m3u8_native" for HLS, "https" for a plain file.</param>
/// <param name="Headers">Headers the site wants with every request for this stream.</param>
public sealed record StreamFormat(string Url, string Protocol, RequestHeaders Headers)
{
    public bool IsHls => Protocol is "m3u8" or "m3u8_native";
}
