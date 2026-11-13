using OuttaContext.Core.Clips;
using OuttaContext.Core.Episodes;
using OuttaContext.Core.Library;
using OuttaContext.Core.Settings;
using OuttaContext.Core.Sources;
using OuttaContext.Core.Storage;
using OuttaContext.Core.Time;
using OuttaContext.TestKit.Assertions;

namespace OuttaContext.TestKit.Builders;

/// <summary>Short ways to make value objects in tests: <c>Values.Time(90)</c>, <c>Values.Span("1:30-1:52")</c>.</summary>
public static class Values
{
    public static VideoTime Time(double seconds) => VideoTime.FromTrusted(seconds);

    public static ClipTimeSpan Span(string text) => ClipTimeSpan.Parse(text).ShouldSucceed();

    public static ClipTitle Title(string text) => ClipTitle.Create(text).ShouldSucceed();

    public static EpisodeName Episode(string text) => EpisodeName.Create(text).ShouldSucceed();

    public static StorageName Storage(string text) => StorageName.Create(text).ShouldSucceed();

    public static VideoQuality Quality(int height) => VideoQuality.FromHeight(height).ShouldSucceed();

    /// <summary>Headers from name/value pairs: <c>Values.Headers(("Referer", "https://p"))</c>.</summary>
    public static RequestHeaders Headers(params (string Name, string Value)[] headers) =>
        RequestHeaders.From(headers.Select(header => new KeyValuePair<string, string>(header.Name, header.Value)));

    /// <summary>A clip ready for <c>repository.AddClip</c>: <paramref name="duration"/> seconds from the start of a YouTube video.</summary>
    public static NewClip NewClip(string title, double duration = 60, string? filePath = null) =>
        new(
            Title(title),
            "youtube",
            "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
            ClipTimeSpan.Create(VideoTime.Zero, Time(duration)).ShouldSucceed(),
            Time(duration),
            filePath ?? $"/c/{title}.mp4",
            Maybe<string>.None);
}
