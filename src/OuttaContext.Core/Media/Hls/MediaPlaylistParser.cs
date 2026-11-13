using System.Globalization;
using System.Text.RegularExpressions;
using OuttaContext.Core.Common;
using OuttaContext.Core.Time;

namespace OuttaContext.Core.Media.Hls;

/// <summary>Reads the text of an HLS media playlist.</summary>
public static partial class MediaPlaylistParser
{
    public static Result<MediaPlaylist, Error> Parse(string text, Uri baseUrl)
    {
        string[] lines = text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();
        if (lines.Length == 0 || lines[0] != "#EXTM3U")
        {
            return MediaErrors.NotAPlaylist();
        }

        if (lines.Any(line => line.StartsWith("#EXT-X-STREAM-INF", StringComparison.Ordinal)))
        {
            return MediaErrors.MasterPlaylist();
        }

        var segments = new List<HlsSegment>();
        Maybe<InitSection> init = Maybe<InitSection>.None;
        bool encrypted = false;
        VideoTime position = VideoTime.Zero;
        Maybe<VideoTime> duration = Maybe<VideoTime>.None;
        Maybe<ByteRange> range = Maybe<ByteRange>.None;
        long nextOffset = 0;

        foreach (string line in lines.Skip(1))
        {
            if (line.StartsWith("#EXTINF:", StringComparison.Ordinal))
            {
                string value = line["#EXTINF:".Length..].Split(',')[0];
                duration = double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds)
                    ? VideoTime.FromSeconds(seconds).ValueOrNone()
                    : Maybe<VideoTime>.None;
            }
            else if (line.StartsWith("#EXT-X-BYTERANGE:", StringComparison.Ordinal))
            {
                Result<ByteRange, Error> parsed = ParseByteRange(line.Split(':', 2)[1], nextOffset);
                if (parsed.IsFailure)
                {
                    return parsed.Error;
                }

                range = parsed.Value;
                nextOffset = parsed.Value.EndExclusive;
            }
            else if (line.StartsWith("#EXT-X-MAP:", StringComparison.Ordinal))
            {
                Dictionary<string, string> attributes = ParseAttributes(line.Split(':', 2)[1]);
                Maybe<ByteRange> mapRange = Maybe<ByteRange>.None;
                if (attributes.TryGetValue("BYTERANGE", out string? mapRangeText))
                {
                    Result<ByteRange, Error> parsed = ParseByteRange(mapRangeText, 0);
                    if (parsed.IsFailure)
                    {
                        return parsed.Error;
                    }

                    mapRange = parsed.Value;
                }

                init = new InitSection(new Uri(baseUrl, attributes["URI"]).ToString(), mapRange);
            }
            else if (line.StartsWith("#EXT-X-KEY:", StringComparison.Ordinal))
            {
                string method = ParseAttributes(line.Split(':', 2)[1]).GetValueOrDefault("METHOD", "NONE");
                encrypted |= !string.Equals(method, "NONE", StringComparison.OrdinalIgnoreCase);
            }
            else if (!line.StartsWith('#'))
            {
                if (!duration.TryGetValue(out VideoTime? length))
                {
                    return MediaErrors.PieceWithoutDuration();
                }

                segments.Add(new HlsSegment(new Uri(baseUrl, line).ToString(), position, length, range));
                position = position.Plus(length);
                duration = Maybe<VideoTime>.None;
                range = Maybe<ByteRange>.None;
            }
        }

        return new MediaPlaylist(segments, init, encrypted);
    }

    /// <summary><c>LENGTH[@OFFSET]</c>; without an offset the range follows the previous one.</summary>
    private static Result<ByteRange, Error> ParseByteRange(string text, long nextOffset)
    {
        Match match = ByteRangeRegex().Match(text.Trim());
        if (!match.Success)
        {
            return MediaErrors.BadByteRange(text);
        }

        long length = long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        long offset = match.Groups[2].Success ? long.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : nextOffset;
        return ByteRange.FromLength(offset, length);
    }

    private static Dictionary<string, string> ParseAttributes(string text) =>
        AttributeRegex().Matches(text)
            .ToDictionary(match => match.Groups[1].Value, match => match.Groups[2].Value.Trim('"'), StringComparer.Ordinal);

    [GeneratedRegex(@"([A-Z0-9-]+)=(""[^""]*""|[^,]*)")]
    private static partial Regex AttributeRegex();

    [GeneratedRegex(@"^(\d+)(?:@(\d+))?$")]
    private static partial Regex ByteRangeRegex();
}
