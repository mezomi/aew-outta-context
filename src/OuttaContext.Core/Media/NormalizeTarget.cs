namespace OuttaContext.Core.Media;

/// <summary>The size, frame rate and codecs every clip is converted to before joining.</summary>
public sealed record NormalizeTarget(
    int Width = 1920,
    int Height = 1080,
    int Fps = 30,
    int AudioRate = 48000,
    string VideoCodec = "libx264",
    string AudioCodec = "aac",
    int Crf = 20,
    string Preset = "veryfast")
{
    public string CacheKey => $"{Width}x{Height}@{Fps}-{VideoCodec}-{Crf}-{AudioRate}";
}
