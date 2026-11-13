using OuttaContext.Core.Clips;
using OuttaContext.Core.Time;

namespace OuttaContext.Core.Library;

/// <summary>The details of a clip about to be saved.</summary>
public sealed record NewClip(
    ClipTitle Title, string Source, string SourceUrl, ClipTimeSpan Span, VideoTime Duration, string FilePath, Maybe<string> PageUrl);
