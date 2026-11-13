using OuttaContext.Core.Common;

namespace OuttaContext.Core.Media;

/// <summary>Errors about streams and playlists that don't come from a specific program.</summary>
public static class MediaErrors
{
    private const string StreamHint = "Use the video page link, or copy a different .m3u8 link from your browser.";

    public static Error NotAPlaylist() => new("hls.not_a_playlist", "The stream link did not return an HLS playlist.", StreamHint);

    public static Error MasterPlaylist() => new("hls.master_playlist", "Expected a media playlist but got a master playlist.", StreamHint);

    public static Error PieceWithoutDuration() => new("hls.no_duration", "The HLS playlist has a piece without a duration.", StreamHint);

    public static Error BadByteRange(string text) => new("hls.byte_range", $"The HLS playlist has a bad byte range: \"{text}\".", StreamHint);

    public static Error Encrypted() =>
        new("hls.encrypted", "This stream is encrypted (DRM); it can't be clipped.", "Try another upload of the same footage.");

    public static Error NoPiecesInRange(string range) =>
        new("hls.no_pieces", $"The stream has no pieces covering {range}.", "Check the range against the video's length.");

    public static Error NoStreams() => new("media.no_streams", "The site offered no stream outta can download.", StreamHint);

    public static Error FfmpegMissing() =>
        new("ffmpeg.missing", "ffmpeg is not installed.", "Install it with `winget install ffmpeg`, then open a new terminal.");
}
