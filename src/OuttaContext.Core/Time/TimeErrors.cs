using OuttaContext.Core.Common;

namespace OuttaContext.Core.Time;

/// <summary>Errors for times and time ranges.</summary>
public static class TimeErrors
{
    private const string FormatHint = "Use START-END, e.g. 90-112, 1:30-1:52 or 1:02:10-1:02:30.";

    public static Error NotATime(string text) =>
        new("time.invalid", $"\"{text}\" is not a time. Use seconds, M:SS or H:MM:SS.", FormatHint);

    public static Error SecondsTooLarge(string text) =>
        new("time.seconds_over_59", $"\"{text}\": seconds must be under 60 in M:SS form.", FormatHint);

    public static Error MinutesTooLarge(string text) =>
        new("time.minutes_over_59", $"\"{text}\": minutes must be under 60 in H:MM:SS form.", FormatHint);

    public static Error Negative(double seconds) =>
        new("time.negative", $"A time can't be negative ({seconds}s).", FormatHint);

    public static Error NotARange(string text) =>
        new("range.invalid", $"\"{text}\" is not a range. Use START-END, e.g. 1:30-1:52.", FormatHint);

    public static Error EndNotAfterStart(string text, VideoTime start, VideoTime end) =>
        new("range.end_before_start", $"\"{text}\": the end ({end}) must be after the start ({start}).", FormatHint);

    public static Error StartPastVideoEnd(VideoTime start, VideoTime videoLength) =>
        new("range.outside_video", $"Start {start} is past the end of the video ({videoLength}).", "Check the video's length and pick a range inside it.");

    public static Error EndPastVideoEnd(VideoTime end, VideoTime videoLength) =>
        new("range.outside_video", $"End {end} is past the end of the video ({videoLength}).", "Check the video's length and pick a range inside it.");
}
