"""outta episode build: normalize → concat → mark built → open next → upload."""

from __future__ import annotations

import hashlib
import shutil
from dataclasses import dataclass
from pathlib import Path

from outta_context.core.errors import ClipFileMissing, EpisodeEmpty, OuttaError
from outta_context.core.naming import safe_filename
from outta_context.core.reporter import Reporter
from outta_context.core.timerange import format_time
from outta_context.library.episodes import EpisodeService
from outta_context.library.models import Clip, Episode
from outta_context.media.ffmpeg import Ffmpeg
from outta_context.media.models import NormalizeTarget
from outta_context.settings.models import Settings
from outta_context.workflows.upload import UploadService


@dataclass(frozen=True)
class BuildResult:
    episode: Episode
    output: Path
    duration: float
    next_episode: Episode | None


class BuildEpisodeWorkflow:
    def __init__(
        self,
        episodes: EpisodeService,
        ffmpeg: Ffmpeg,
        settings: Settings,
        cache_dir: Path,
        reporter: Reporter,
        uploader: UploadService | None = None,
        target: NormalizeTarget | None = None,
    ) -> None:
        self.episodes = episodes
        self.ffmpeg = ffmpeg
        self.settings = settings
        self.cache_dir = cache_dir
        self.reporter = reporter
        self.uploader = uploader
        self.target = target or NormalizeTarget()

    def cache_path(self, clip: Clip) -> Path:
        stat = clip.file_path.stat()
        key = f"{clip.file_path.resolve()}|{stat.st_size}|{stat.st_mtime_ns}|{self.target.key()}"
        digest = hashlib.sha1(key.encode("utf-8")).hexdigest()[:16]
        return self.cache_dir / "normalized" / f"clip{clip.id}-{digest}.mp4"

    def run(
        self,
        name: str | None = None,
        upload: bool = False,
        storage: str | None = None,
        clean_local: bool = False,
    ) -> BuildResult:
        r = self.reporter
        status = self.episodes.status(name)
        episode, clips = status.episode, status.clips
        if not clips:
            raise EpisodeEmpty(f'"{episode.name}" has no clips.')
        missing = [c for c in clips if not c.file_path.exists()]
        if missing:
            c = missing[0]
            raise ClipFileMissing(f'Clip {c.id} ("{c.title}") is missing its file: {c.file_path}')
        if clean_local and not upload:
            raise OuttaError("--clean-local only works with --upload.", "Add --upload.")
        self.ffmpeg.ensure_available()

        r.step(f'Building "{episode.name}": {len(clips)} clips, {format_time(status.total)}')
        t = self.target
        r.detail(f"Target: {t.width}x{t.height}, {t.fps} fps, H.264, AAC {t.audio_rate} Hz")
        normalized: list[Path] = []
        for index, clip in enumerate(clips, start=1):
            cached = self.cache_path(clip)
            if cached.exists():
                r.step(f'Clip {index}/{len(clips)} "{clip.title}": using converted copy')
            else:
                r.step(f'Converting clip {index}/{len(clips)} "{clip.title}"')
                self.ffmpeg.normalize(clip.file_path, cached, self.target)
            normalized.append(cached)

        output = self.settings.episodes_folder / f"{safe_filename(episode.name)}.mp4"
        r.step("Joining clips")
        self.ffmpeg.concat(normalized, output, self.cache_dir / "work")
        duration = self.ffmpeg.probe(output).duration
        built, opened = self.episodes.finish(episode, output)
        r.success(f"Built {output} ({format_time(duration)})")
        if opened is not None:
            r.success(f'Opened "{opened.name}" for new clips')

        if upload:
            if self.uploader is None:
                raise OuttaError("Uploading is not available.")
            self.uploader.upload_episode(built, clips, storage)
            if clean_local:
                self._clean(clips, normalized)
        return BuildResult(built, output, duration, opened)

    def _clean(self, clips: list[Clip], normalized: list[Path]) -> None:
        for clip in clips:
            clip.file_path.unlink(missing_ok=True)
        for path in normalized:
            path.unlink(missing_ok=True)
        shutil.rmtree(self.cache_dir / "work", ignore_errors=True)
        self.reporter.success(f"Deleted {len(clips)} local clip files (they are uploaded)")
