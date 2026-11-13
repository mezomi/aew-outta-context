using System.Globalization;
using System.Text.RegularExpressions;
using OuttaContext.Core.Results;

namespace OuttaContext.Core.Media.Hls;

/// <summary>Reads the text of an HLS media playlist.</summary>
public static partial class MediaPlaylistParser
{
    private const string Tool = "HLS";

    public static ToolResult<MediaPlaylist> Parse(string text, Uri baseUrl)
    {
        string[] lines = text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();
        if (lines.Length == 0 || lines[0] != "#EXTM3U")
        {
            return Fail("The stream link did not return an HLS playlist.");
        }

        if (lines.Any(line => line.StartsWith("#EXT-X-STREAM-INF", StringComparison.Ordinal)))
        {
            return Fail("Expected a media playlist but got a master playlist.");
        }

        var segments = new List<HlsSegment>();
        string? initUrl = null;
        ByteRange? initRange = null;
        bool encrypted = false;
        double position = 0;
        double? duration = null;
        ByteRange? range = null;
        long nextOffset = 0;

        foreach (string line in lines.Skip(1))
        {
            if (line.StartsWith("#EXTINF:", StringComparison.Ordinal))
            {
                string value = line["#EXTINF:".Length..].Split(',')[0];
                duration = double.Parse(value, CultureInfo.InvariantCulture);
            }
            else if (line.StartsWith("#EXT-X-BYTERANGE:", StringComparison.Ordinal))
            {
                (range, nextOffset) = ParseByteRange(line.Split(':', 2)[1], nextOffset);
            }
            else if (line.StartsWith("#EXT-X-MAP:", StringComparison.Ordinal))
            {
                Dictionary<string, string> attributes = ParseAttributes(line.Split(':', 2)[1]);
                initUrl = new Uri(baseUrl, attributes["URI"]).ToString();
                if (attributes.TryGetValue("BYTERANGE", out string? mapRange))
                {
                    (initRange, _) = ParseByteRange(mapRange, 0);
                }
            }
            else if (line.StartsWith("#EXT-X-KEY:", StringComparison.Ordinal))
            {
                string method = ParseAttributes(line.Split(':', 2)[1]).GetValueOrDefault("METHOD", "NONE");
                encrypted |= !string.Equals(method, "NONE", StringComparison.OrdinalIgnoreCase);
            }
            else if (!line.StartsWith('#'))
            {
                if (duration is not { } length)
                {
                    return Fail("The HLS playlist has a piece without a duration.");
                }

                segments.Add(new HlsSegment(new Uri(baseUrl, line).ToString(), position, length, range));
                position += length;
                duration = null;
                range = null;
            }
        }

        return ToolResult<MediaPlaylist>.Success(new MediaPlaylist(segments, initUrl, initRange, encrypted));
    }

    private static (ByteRange Range, long NextOffset) ParseByteRange(string text, long nextOffset)
    {
        Match match = ByteRangeRegex().Match(text.Trim());
        long length = long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        long offset = match.Groups[2].Success
            ? long.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture)
            : nextOffset;
        return (new ByteRange(offset, offset + length - 1), offset + length);
    }

    private static Dictionary<string, string> ParseAttributes(string text) =>
        AttributeRegex().Matches(text)
            .ToDictionary(match => match.Groups[1].Value, match => match.Groups[2].Value.Trim('"'), StringComparer.Ordinal);

    private static ToolError Fail(string message) =>
        new(Tool, message, "Use the video page link, or copy a different .m3u8 link from your browser.");

    [GeneratedRegex(@"([A-Z0-9-]+)=(""[^""]*""|[^,]*)")]
    private static partial Regex AttributeRegex();

    [GeneratedRegex(@"^(\d+)(?:@(\d+))?$")]
    private static partial Regex ByteRangeRegex();
}
