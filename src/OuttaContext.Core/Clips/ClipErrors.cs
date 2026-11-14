using OuttaContext.Core.Common;
using OuttaContext.Core.Time;

namespace OuttaContext.Core.Clips;

/// <summary>Errors for clip titles and the clip cap.</summary>
public static class ClipErrors
{
    private const string TitleHint = "Give the clip a short title with letters or numbers, e.g. --title \"Table spot\".";

    public static Error EmptyTitle() => new("clip.title_empty", "The clip title is empty.", TitleHint);

    public static Error TitleHasNoFileName(string title) =>
        new("clip.title_unusable", $"\"{title}\" has no usable characters for a file name.", TitleHint);

    public static Error DuplicateTitle(ClipTitle title, long existingId) =>
        new(
            "clip.title_taken",
            $"A clip titled \"{title}\" already exists (ID {existingId}).",
            "Pick a different --title, or add --replace to replace that clip with this one.");

    public static Error TitleTakenByAnother(ClipTitle title, long existingId, long replacingId) =>
        new(
            "clip.title_taken",
            $"Clip {existingId} is already titled \"{title}\", so clip {replacingId} can't take that title.",
            $"Pick a different --title, or use --replace {existingId} to replace clip {existingId} instead.");

    public static Error NothingToReplace(ClipTitle title) =>
        new(
            "clip.nothing_to_replace",
            $"There is no clip titled \"{title}\" to replace.",
            "Check the title (`outta episode status` lists clips), use --replace ID, or drop --replace to make a new clip.");

    public static Error CannotReplaceFile(string path, string problem) =>
        new(
            "clip.replace_failed",
            $"Could not replace {path}: {problem}",
            "Close any program that has the clip open (a video player or editor), then try again. The old clip is unchanged.");

    public static Error FileExists(string path) =>
        new("clip.file_exists", $"{path} already exists.", "Pick a different --title, or move that file away.");

    public static Error TooLong(ClipTimeSpan span, VideoTime cap) =>
        new(
            "clip.too_long",
            $"{span} is {span.Length.ToShortLength()} long, over the {cap.ToShortLength()} clip cap.",
            "Check the range for a typo. If you really want a longer clip, raise the cap, " +
            "e.g. `outta config set max-clip-length 1:30` (or `off`).");

    public static Error NotFound(string idOrTitle) =>
        new("clip.not_found", $"No clip with ID or title \"{idOrTitle}\".", "`outta episode status` lists clips and their IDs.");

    public static Error NotFound(long id) =>
        new("clip.not_found", $"There is no clip with ID {id} to replace.", "`outta episode status` lists clips and their IDs.");

    public static Error FileMissing(string path) =>
        new("clip.file_missing", $"The clip file is missing: {path}", "Restore the file or remove the clip with `outta episode remove CLIP_ID`.");
}
