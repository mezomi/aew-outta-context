using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using OuttaContext.Core.Library;
using OuttaContext.Core.Media;
using OuttaContext.Core.Paths;

namespace OuttaContext.Core.Episodes;

/// <summary>
/// Where converted copies of clips are kept, so rebuilding an episode after a reorder only joins.
/// A copy's name changes when the clip file or the target format changes.
/// </summary>
public sealed class EpisodeCache(AppPaths paths, NormalizeTarget target)
{
    public NormalizeTarget Target => target;

    public string WorkDirectory => Path.Combine(paths.CacheDirectory, "work");

    public string PathFor(Clip clip)
    {
        var file = new FileInfo(clip.FilePath);
        string key = string.Create(
            CultureInfo.InvariantCulture,
            $"{file.FullName}|{file.Length}|{file.LastWriteTimeUtc.Ticks}|{target.CacheKey}");
        string digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..16];
        return Path.Combine(paths.CacheDirectory, "normalized", string.Create(CultureInfo.InvariantCulture, $"clip{clip.Id}-{digest}.mp4"));
    }
}
