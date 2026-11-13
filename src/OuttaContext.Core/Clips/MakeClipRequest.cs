namespace OuttaContext.Core.Clips;

/// <summary>Everything `outta clip` was asked to do, as typed by the user (checked by the handler).</summary>
public sealed record MakeClipRequest(
    string Url,
    string Range,
    string Title,
    string? Episode = null,
    bool NoEpisode = false,
    bool Upload = false,
    string? Storage = null,
    string? Referer = null);
