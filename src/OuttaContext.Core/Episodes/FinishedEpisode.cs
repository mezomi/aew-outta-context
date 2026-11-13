using OuttaContext.Core.Library;

namespace OuttaContext.Core.Episodes;

/// <summary>An episode marked built, and the episode opened for new clips (none if one was already open).</summary>
public sealed record FinishedEpisode(Episode Built, Maybe<Episode> Opened);
