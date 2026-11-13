using OuttaContext.Core.Library;

namespace OuttaContext.Core.Episodes;

/// <summary>A clip taken out of its episode, and how that episode looks now.</summary>
public sealed record RemovedClip(Clip Clip, EpisodeStatus Status);
