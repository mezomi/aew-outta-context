"""Every error the tool raises on purpose. Each carries a message and a hint on how to fix it."""

from __future__ import annotations


class OuttaError(Exception):
    """Base error: `message` says what went wrong, `hint` says how to fix it."""

    default_hint: str = ""

    def __init__(self, message: str, hint: str | None = None) -> None:
        super().__init__(message)
        self.message = message
        self.hint = hint if hint is not None else self.default_hint

    def __str__(self) -> str:
        return self.message


class InvalidTimeRange(OuttaError):
    default_hint = "Use START-END, e.g. 90-112, 1:30-1:52 or 1:02:10-1:02:30."


class RangeOutsideVideo(OuttaError):
    default_hint = "Check the video's length and pick a range inside it."


class ClipTooLong(OuttaError):
    default_hint = (
        "Check the range for a typo. If you really want a longer clip, raise the cap, "
        "e.g. `outta config set max-clip-length 1:30` (or `off`)."
    )


class InvalidTitle(OuttaError):
    default_hint = 'Give the clip a short title with letters or numbers, e.g. --title "Table spot".'


class DuplicateTitle(OuttaError):
    default_hint = "Pick a different --title. Every clip title must be unique."


class UnsupportedUrl(OuttaError):
    default_hint = (
        "Use a Dailymotion or YouTube video link. `outta scrape` can find them on a page."
    )


class NoVideosFound(OuttaError):
    default_hint = (
        "The page may load its player with JavaScript. Open it in your browser, play the video, "
        "and copy the video link (or right-click the player) instead. If the site uses a new embed "
        "style, register it with `outta scrape prefixes add SITE PREFIX`."
    )


class FetchFailed(OuttaError):
    default_hint = "Check the address and your internet connection, then try again."


class DuplicatePrefix(OuttaError):
    default_hint = "See registered prefixes with `outta scrape prefixes list`."


class PrefixNotFound(OuttaError):
    default_hint = "See registered prefixes with `outta scrape prefixes list`."


class UnknownSite(OuttaError):
    pass


class DownloadFailed(OuttaError):
    default_hint = (
        "Sites change often: update the tool (and yt-dlp) and run `outta doctor`. "
        "For embed-only videos pass --referer PAGE_URL."
    )


class FfmpegMissing(OuttaError):
    default_hint = "Install ffmpeg (Windows: `winget install ffmpeg`), then open a new terminal."


class FfmpegFailed(OuttaError):
    default_hint = "Rerun with --verbose to see the ffmpeg command and its output."


class ClipNotFound(OuttaError):
    default_hint = "See clip IDs with `outta episode status`."


class ClipFileMissing(OuttaError):
    default_hint = "Restore the file or remove the clip with `outta episode remove CLIP_ID`."


class EpisodeNotFound(OuttaError):
    default_hint = "See episodes with `outta episode list`."


class EpisodeExists(OuttaError):
    default_hint = "Pick another name, or see episodes with `outta episode list`."


class EpisodeEmpty(OuttaError):
    default_hint = "Add clips first with `outta clip URL --range START-END --title TITLE`."


class EpisodeBuilt(OuttaError):
    default_hint = "That episode is already built. Add clips to the current episode instead."


class InvalidPosition(OuttaError):
    default_hint = "Positions start at 1. See the order with `outta episode status`."


class SettingsError(OuttaError):
    default_hint = "Fix or delete the settings file; `outta config show` prints where it is."


class StorageNotConfigured(OuttaError):
    default_hint = (
        "Add one with `outta storage add NAME --provider gdrive` and set it as default "
        "with `outta storage default NAME`."
    )


class StorageExists(OuttaError):
    default_hint = "Pick another name, or remove the old one with `outta storage remove NAME`."


class StorageAuthFailed(OuttaError):
    default_hint = (
        "The login may have expired or been revoked. Remove it with `outta storage remove NAME` "
        "and add it again."
    )


class RcloneMissing(OuttaError):
    default_hint = (
        "Install rclone (Windows: `winget install Rclone.Rclone`), then open a new terminal."
    )


class UploadFailed(OuttaError):
    default_hint = "Check your connection and run `outta storage test NAME`."


class CredentialStoreUnavailable(OuttaError):
    default_hint = (
        "No system keychain was found. On Windows this uses Credential Manager; on Linux install "
        "a Secret Service provider such as gnome-keyring."
    )
