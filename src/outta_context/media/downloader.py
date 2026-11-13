"""ClipDownloader wraps yt-dlp. The only module that calls yt-dlp, and where its errors are translated."""

from __future__ import annotations

import re
from collections.abc import Callable
from pathlib import Path
from typing import Any

from outta_context.core.errors import DownloadFailed
from outta_context.core.timerange import TimeRange
from outta_context.media.models import DownloadedClip, VideoInfo

ProgressCallback = Callable[[float, float | None], None]
DetailCallback = Callable[[str], None]


def _default_factory(options: dict[str, Any]) -> Any:
    import yt_dlp

    return yt_dlp.YoutubeDL(options)


def format_selector(max_quality: int) -> str:
    h = int(max_quality)
    return f"bv*[height<={h}][ext=mp4]+ba[ext=m4a]/bv*[height<={h}]+ba/b[height<={h}]/bv*+ba/b"


_BOILERPLATE = re.compile(r";?\s*please report this issue on.*$", re.I | re.S)


def translate_error(message: str) -> DownloadFailed:
    text = _BOILERPLATE.sub("", message.removeprefix("ERROR: ")).strip()
    text = re.sub(r"\s*\(caused by .*\)$", "", text, flags=re.S)
    low = text.lower()
    if any(
        w in low
        for w in (
            "unable to connect",
            "proxy",
            "timed out",
            "connection",
            "getaddrinfo",
            "name resolution",
            "network is unreachable",
        )
    ):
        return DownloadFailed(f"Network problem: {text}", "Check your connection and try again.")
    if "geo" in low and ("restrict" in low or "not available in your" in low or "country" in low):
        return DownloadFailed(
            f"The video is blocked in your country: {text}",
            "Try another upload of the same footage.",
        )
    if any(
        w in low
        for w in (
            "private",
            "has been removed",
            "no longer available",
            "404",
            "video unavailable",
            "does not exist",
        )
    ):
        return DownloadFailed(f"The video is not available: {text}", "Check the link in a browser.")
    if "sign in" in low or "login" in low or ("age" in low and "restrict" in low):
        return DownloadFailed(
            f"The site wants a login for this video: {text}",
            "Update yt-dlp (`outta doctor` shows how). If it persists, try another upload.",
        )
    if "ffmpeg" in low and ("not found" in low or "not installed" in low):
        return DownloadFailed(
            f"yt-dlp needs ffmpeg: {text}",
            "Install ffmpeg (Windows: `winget install ffmpeg`), then open a new terminal.",
        )
    if "ffmpeg exited" in low:
        return DownloadFailed(
            f"ffmpeg could not cut this video: {text}",
            "Rerun with --verbose to see why. Updating yt-dlp often helps with site changes.",
        )
    if "unable to download" in low or "http error 5" in low:
        return DownloadFailed(f"Network problem: {text}", "Check your connection and try again.")
    return DownloadFailed(f"Download failed: {text}")


class _Logger:
    def __init__(self, detail: DetailCallback | None) -> None:
        self._detail = detail
        self.errors: list[str] = []

    def debug(self, msg: str) -> None:
        if self._detail and not msg.startswith("[debug] "):
            self._detail(msg)

    def info(self, msg: str) -> None:
        if self._detail:
            self._detail(msg)

    def warning(self, msg: str) -> None:
        if self._detail:
            self._detail(f"yt-dlp warning: {msg}")

    def error(self, msg: str) -> None:
        self.errors.append(msg)


class ClipDownloader:
    def __init__(
        self,
        ydl_factory: Callable[[dict[str, Any]], Any] = _default_factory,
        ffmpeg_location: str | None = None,
    ) -> None:
        self._factory = ydl_factory
        self._ffmpeg_location = ffmpeg_location

    def _base_options(self, referer: str | None, detail: DetailCallback | None) -> dict[str, Any]:
        options: dict[str, Any] = {
            "quiet": True,
            "no_warnings": False,
            "noprogress": True,
            "noplaylist": True,
            "logger": _Logger(detail),
        }
        if referer:
            options["http_headers"] = {"Referer": referer}
        if self._ffmpeg_location:
            options["ffmpeg_location"] = self._ffmpeg_location
        return options

    def build_download_options(
        self,
        output_stem: Path,
        time_range: TimeRange,
        max_quality: int,
        referer: str | None = None,
        detail: DetailCallback | None = None,
        progress: ProgressCallback | None = None,
    ) -> dict[str, Any]:
        from yt_dlp.utils import download_range_func

        options = self._base_options(referer, detail)
        options.update(
            {
                "format": format_selector(max_quality),
                "merge_output_format": "mp4",
                "outtmpl": {"default": f"{output_stem}.%(ext)s"},
                "download_ranges": download_range_func(None, [(time_range.start, time_range.end)]),
                "force_keyframes_at_cuts": True,
                "overwrites": True,
                "postprocessors": [{"key": "FFmpegVideoRemuxer", "preferedformat": "mp4"}],
            }
        )
        if progress is not None:

            def hook(status: dict[str, Any]) -> None:
                if status.get("status") in ("downloading", "finished"):
                    done = float(status.get("downloaded_bytes") or 0)
                    total = status.get("total_bytes") or status.get("total_bytes_estimate")
                    progress(done, float(total) if total else None)

            options["progress_hooks"] = [hook]
        return options

    def get_info(
        self, url: str, referer: str | None = None, detail: DetailCallback | None = None
    ) -> VideoInfo:
        options = self._base_options(referer, detail)
        options["skip_download"] = True
        try:
            with self._factory(options) as ydl:
                raw = ydl.extract_info(url, download=False)
        except Exception as exc:
            raise translate_error(str(exc)) from exc
        if not raw:
            raise DownloadFailed(f"yt-dlp returned no information for {url}")
        if raw.get("_type") == "playlist":
            raise DownloadFailed(f"{url} is a playlist.", "Use the link of a single video.")
        duration = raw.get("duration")
        return VideoInfo(
            video_id=str(raw.get("id", "")),
            title=str(raw.get("title") or "untitled"),
            duration=float(duration) if duration else None,
            webpage_url=str(raw.get("webpage_url") or url),
            extractor=str(raw.get("extractor_key") or raw.get("extractor") or ""),
        )

    def download(
        self,
        url: str,
        output_stem: Path,
        time_range: TimeRange,
        max_quality: int,
        referer: str | None = None,
        detail: DetailCallback | None = None,
        progress: ProgressCallback | None = None,
    ) -> DownloadedClip:
        options = self.build_download_options(
            output_stem, time_range, max_quality, referer, detail, progress
        )
        output_stem.parent.mkdir(parents=True, exist_ok=True)
        try:
            with self._factory(options) as ydl:
                code = ydl.download([url])
        except Exception as exc:
            raise translate_error(str(exc)) from exc
        logger: _Logger = options["logger"]
        if code:
            raise translate_error(
                logger.errors[-1] if logger.errors else "yt-dlp reported an error"
            )
        final = output_stem.with_name(output_stem.name + ".mp4")
        if not final.exists():
            candidates = sorted(output_stem.parent.glob(f"{_glob_escape(output_stem.name)}.*"))
            candidates = [c for c in candidates if c.suffix.lower() in (".mp4", ".mkv", ".webm")]
            if not candidates:
                raise DownloadFailed("yt-dlp finished but no video file was written.")
            final = candidates[0]
        return DownloadedClip(path=final, size_bytes=final.stat().st_size)


def _glob_escape(text: str) -> str:
    return "".join(f"[{c}]" if c in "[]*?" else c for c in text)
