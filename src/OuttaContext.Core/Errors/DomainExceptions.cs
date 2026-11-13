namespace OuttaContext.Core.Errors;

// Every rule the app enforces has its own exception type, each with a default hint.
// They are grouped in one file because each is a one-liner.
#pragma warning disable CA1032 // Standard constructors: these always need a message and hint.

public sealed class InvalidTimeRangeException(string message, string? hint = null)
    : OuttaException(message, hint ?? "Use START-END, e.g. 90-112, 1:30-1:52 or 1:02:10-1:02:30.");

public sealed class RangeOutsideVideoException(string message)
    : OuttaException(message, "Check the video's length and pick a range inside it.");

public sealed class ClipTooLongException(string message)
    : OuttaException(
        message,
        "Check the range for a typo. If you really want a longer clip, raise the cap, " +
        "e.g. `outta config set max-clip-length 1:30` (or `off`).");

public sealed class InvalidTitleException(string message)
    : OuttaException(message, "Give the clip a short title with letters or numbers, e.g. --title \"Table spot\".");

public sealed class DuplicateTitleException(string message, string? hint = null)
    : OuttaException(message, hint ?? "Pick a different --title. Every clip title must be unique.");

public sealed class UnsupportedUrlException(string message)
    : OuttaException(message, "Use a Dailymotion or YouTube video link, or a direct .m3u8/.mp4 link. `outta scrape` can find links on a page.");

public sealed class NoVideosFoundException(string message)
    : OuttaException(
        message,
        "The page may load its player with JavaScript. Play the video in your browser and copy its link instead. " +
        "If the site uses a new embed style, register it with `outta scrape prefixes add SITE PREFIX`.");

public sealed class SettingsException(string message, string? hint = null)
    : OuttaException(message, hint ?? "Fix or delete the settings file; `outta config show` prints where it is.");

public sealed class NotFoundException(string message, string hint)
    : OuttaException(message, hint);

public sealed class AlreadyExistsException(string message, string hint)
    : OuttaException(message, hint);

public sealed class EpisodeBuiltException(string message)
    : OuttaException(message, "That episode is already built. Add clips to the current episode instead.");

public sealed class EpisodeEmptyException(string message)
    : OuttaException(message, "Add clips first with `outta clip URL --range START-END --title TITLE`.");

public sealed class ClipFileMissingException(string message)
    : OuttaException(message, "Restore the file or remove the clip with `outta episode remove CLIP_ID`.");

public sealed class StorageNotConfiguredException(string message)
    : OuttaException(
        message,
        "Add one with `outta storage add NAME --provider gdrive`, then `outta storage default NAME`.");

#pragma warning restore CA1032
