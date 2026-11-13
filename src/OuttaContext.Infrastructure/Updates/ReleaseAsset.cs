using System.Text.Json.Serialization;

namespace OuttaContext.Infrastructure.Updates;

/// <summary>A file attached to a GitHub release.</summary>
/// <param name="Digest">"sha256:..." when GitHub computed one.</param>
public sealed record ReleaseAsset(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("browser_download_url")] string DownloadUrl,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("digest")] string? Digest)
{
    /// <summary>The SHA-256 GitHub published for this file, or none.</summary>
    public Maybe<string> Sha256 =>
        Digest is { Length: > 0 } digest ? digest.Replace("sha256:", string.Empty, StringComparison.Ordinal) : Maybe<string>.None;
}
