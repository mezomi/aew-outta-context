using OuttaContext.Core.Episodes;
using OuttaContext.Core.Library;

namespace OuttaContext.Core.Clips;

/// <summary>The saved clip, and the episode it went into (none with --no-episode).</summary>
public sealed record MakeClipResult(Clip Clip, Maybe<EpisodeStatus> Status);
