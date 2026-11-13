using OuttaContext.Core.Episodes;
using OuttaContext.Core.Time;

namespace OuttaContext.Core.Library;

/// <summary>An episode: an ordered list of clips with a target length.</summary>
/// <param name="OutputPath">The built MP4, once built.</param>
public sealed record Episode(
    long Id,
    EpisodeName Name,
    VideoTime TargetLength,
    EpisodeState State,
    Maybe<string> OutputPath,
    string CreatedAt,
    Maybe<string> BuiltAt)
{
    public bool IsBuilt => State == EpisodeState.Built;
}
