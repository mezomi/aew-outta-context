using OuttaContext.Core.Common;
using OuttaContext.Core.Library;

namespace OuttaContext.Core.Episodes;

/// <summary>Errors for episodes.</summary>
public static class EpisodeErrors
{
    public static Error EmptyName() =>
        new("episode.name_empty", "The episode name is empty.", "Give it a name, e.g. \"Episode 4\".");

    public static Error NameHasNoFileName(string name) =>
        new("episode.name_unusable", $"\"{name}\" has no usable characters for a file name.", "Give it a name with letters or numbers, e.g. \"Episode 4\".");

    public static Error NotFound(EpisodeName name) =>
        new("episode.not_found", $"There is no episode named \"{name}\".", "See episodes with `outta episode list`.");

    public static Error NameTaken(EpisodeName name) =>
        new("episode.exists", $"An episode named \"{name}\" already exists.", "Pick another name.");

    public static Error AlreadyBuilt(Episode episode) =>
        new("episode.built", $"\"{episode.Name}\" is already built.", "That episode is already built. Add clips to the current episode instead.");

    public static Error Empty(Episode episode) =>
        new("episode.empty", $"\"{episode.Name}\" has no clips.", "Add clips first with `outta clip URL --range START-END --title TITLE`.");

    public static Error ClipFileMissing(Clip clip) =>
        new(
            "episode.clip_file_missing",
            $"Clip {clip.Id} (\"{clip.Title}\") is missing its file: {clip.FilePath}",
            "Restore the file or remove the clip with `outta episode remove CLIP_ID`.");

    public static Error ClipNotFound(long clipId) =>
        new("episode.clip_not_found", $"There is no clip with ID {clipId}.", "See clip IDs with `outta episode status`.");

    public static Error ClipNotInEpisode(Clip clip) =>
        new("episode.clip_not_in_episode", $"Clip {clip.Id} (\"{clip.Title}\") is not in an episode.", "See clip IDs with `outta episode status`.");

    public static Error PositionOutOfRange(int position, int count, Episode episode) =>
        new(
            "episode.position",
            $"Position {position} is outside 1-{count} for \"{episode.Name}\".",
            "Positions start at 1. See the order with `outta episode status`.");

    public static Error CleanLocalNeedsUpload() =>
        new("episode.clean_local", "--clean-local only works with --upload.", "Add --upload.");
}
