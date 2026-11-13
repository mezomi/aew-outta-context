using OuttaContext.Core.Library;
using OuttaContext.Core.Time;

namespace OuttaContext.Core.Episodes;

/// <summary>The built episode, its file and length, and the episode opened next (if any).</summary>
public sealed record BuildEpisodeResult(Episode Episode, string OutputPath, VideoTime Duration, Maybe<Episode> NextEpisode);
