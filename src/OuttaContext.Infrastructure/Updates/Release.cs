using System.Text.Json.Serialization;

namespace OuttaContext.Infrastructure.Updates;

/// <summary>The parts of GitHub's "latest release" answer we use.</summary>
public sealed record Release(
    [property: JsonPropertyName("tag_name")] string Tag,
    [property: JsonPropertyName("assets")] IReadOnlyList<ReleaseAsset> Assets)
{
    public Maybe<ReleaseAsset> Asset(string name) => Assets.TryFirst(asset => asset.Name == name);
}
