namespace OuttaContext.Core.Episodes;

/// <summary>What `outta episode build` was asked to do. No name means the current episode.</summary>
public sealed record BuildEpisodeRequest(string? Name = null, bool Upload = false, string? Storage = null, bool CleanLocal = false);
