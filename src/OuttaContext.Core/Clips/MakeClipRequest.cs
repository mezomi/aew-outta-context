using OuttaContext.Core.Library;

namespace OuttaContext.Core.Clips;

/// <summary>Everything `outta clip` was asked to do.</summary>
public sealed record MakeClipRequest(
    string Url,
    string Range,
    string Title,
    string? Episode = null,
    bool NoEpisode = false,
    bool Upload = false,
    string? Storage = null,
    string? Referer = null);

/// <summary>The saved clip, and the episode it went into (null with --no-episode).</summary>
public sealed record MakeClipResult(Clip Clip, EpisodeStatus? Status);
