"""outta clip: detect → info → check range → download → record → add to episode → upload."""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

from outta_context.core.errors import (
    ClipTooLong,
    DuplicateTitle,
    EpisodeBuilt,
    FfmpegFailed,
    OuttaError,
)
from outta_context.core.naming import clean_title, safe_filename
from outta_context.core.reporter import Reporter
from outta_context.core.timerange import format_length, format_time, parse_range
from outta_context.library.episodes import EpisodeService
from outta_context.library.models import Clip, EpisodeStatus
from outta_context.library.repository import LibraryRepository
from outta_context.media.downloader import ClipDownloader
from outta_context.media.ffmpeg import Ffmpeg
from outta_context.settings.models import Settings
from outta_context.sources.registry import SourceRegistry
from outta_context.workflows.upload import UploadService


@dataclass(frozen=True)
class ClipRequest:
    url: str
    range_text: str
    title: str
    episode: str | None = None
    no_episode: bool = False
    upload: bool = False
    referer: str | None = None
    storage: str | None = None


@dataclass(frozen=True)
class ClipResult:
    clip: Clip
    status: EpisodeStatus | None


def describe_status(status: EpisodeStatus) -> str:
    target = format_time(status.episode.target_length)
    line = f'Added to "{status.episode.name}": {format_time(status.total)} of {target}'
    if status.ready:
        return line + " (ready to build)"
    return line + f" ({format_time(status.remaining)} left)"


class MakeClipWorkflow:
    def __init__(
        self,
        sources: SourceRegistry,
        downloader: ClipDownloader,
        ffmpeg: Ffmpeg,
        repo: LibraryRepository,
        episodes: EpisodeService,
        settings: Settings,
        reporter: Reporter,
        uploader: UploadService | None = None,
    ) -> None:
        self.sources = sources
        self.downloader = downloader
        self.ffmpeg = ffmpeg
        self.repo = repo
        self.episodes = episodes
        self.settings = settings
        self.reporter = reporter
        self.uploader = uploader

    def run(self, request: ClipRequest) -> ClipResult:
        r = self.reporter
        # Cheap checks first, so nothing is downloaded for a request that can't succeed.
        title = clean_title(request.title)
        stem = safe_filename(title)
        existing = self.repo.find_clip_by_title(title)
        if existing is not None:
            raise DuplicateTitle(
                f'A clip titled "{existing.title}" already exists (ID {existing.id}).'
            )
        output = self.settings.clip_folder / f"{stem}.mp4"
        if output.exists():
            raise DuplicateTitle(
                f"{output} already exists.", "Pick a different --title, or move that file away."
            )
        time_range = parse_range(request.range_text)
        cap = self.settings.max_clip_length
        if cap and time_range.length > cap + 1e-6:
            raise ClipTooLong(
                f"{time_range} is {format_length(time_range.length)} long, over the "
                f"{format_length(cap)} clip cap."
            )
        source = self.sources.detect(request.url)
        url = source.canonical_url(request.url)
        if not request.no_episode:
            target = self.episodes.get(request.episode)
            if target.is_built:
                raise EpisodeBuilt(f'"{target.name}" is already built.')
        referer = request.referer or self.repo.referer_for(url)

        r.step(f"Detected source: {source.label}")
        if referer:
            r.detail(f"Referer: {referer}")
        r.step("Reading video info...")
        info = self.downloader.get_info(url, referer=referer, detail=r.detail)
        length = f" ({format_time(info.duration)})" if info.duration else ""
        r.step(f'Video: "{info.title}"{length}')
        if info.duration is None:
            r.warn("The site did not report the video's length; the range can't be checked first.")
        time_range.check_within(info.duration)

        r.step(
            f"Downloading {time_range} ({format_length(time_range.length)}, "
            f"best up to {self.settings.max_quality}p, exact cuts)"
        )
        with r.progress(title) as task:
            downloaded = self.downloader.download(
                url,
                output.with_suffix(""),
                time_range,
                self.settings.max_quality,
                referer=referer,
                detail=r.detail,
                progress=task.update,
            )
        duration = self._measure(downloaded.path, time_range.length)
        clip = self.repo.add_clip(
            title=title,
            source=source.name,
            source_url=url,
            start=time_range.start,
            end=time_range.end,
            duration=duration,
            file_path=downloaded.path,
            page_url=referer,
        )
        r.success(f"Saved {downloaded.path} (clip {clip.id}, {format_length(duration)})")

        status = None
        if not request.no_episode:
            status = self.episodes.add_clip(clip, request.episode)
            r.success(describe_status(status))
            if status.ready:
                r.warn(
                    f'"{status.episode.name}" has reached its target length. '
                    f"Build it with `outta episode build`."
                )
        if request.upload:
            if self.uploader is None:
                raise OuttaError("Uploading is not available.")
            self.uploader.upload_clip(clip, request.storage)
        return ClipResult(clip, status)

    def _measure(self, path: Path, fallback: float) -> float:
        if not self.ffmpeg.available():
            self.reporter.detail("ffprobe not found; using the requested length")
            return fallback
        try:
            measured = self.ffmpeg.probe(path).duration
        except (FfmpegFailed, ValueError):
            return fallback
        return measured or fallback
