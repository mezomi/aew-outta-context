using OuttaContext.Core.Common;
using OuttaContext.Core.Library;

namespace OuttaContext.Core.Uploads;

/// <summary>Errors for `outta upload` and --upload.</summary>
public static class UploadErrors
{
    public static Error FileMissing(string path) =>
        new("upload.file_missing", $"{path} does not exist.", "Check the path, clip ID or episode name.");

    public static Error EpisodeNotBuilt(Episode episode) =>
        new("upload.not_built", $"\"{episode.Name}\" has no built video yet.", "Build it with `outta episode build`.");

    public static Error NothingMatches(string target) =>
        new("upload.unknown_target", $"\"{target}\" is not a file, a clip ID or title, or an episode name.", "`outta episode status` lists clips; `outta episode list` lists episodes.");
}
