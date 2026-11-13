"""Upload files, clips and episodes to a configured storage, skipping repeats."""

from __future__ import annotations

from pathlib import Path

from outta_context.core.errors import ClipFileMissing, StorageNotConfigured, UploadFailed
from outta_context.core.naming import safe_filename
from outta_context.core.reporter import Reporter
from outta_context.library.models import Clip, Episode
from outta_context.library.repository import LibraryRepository
from outta_context.settings.models import Settings
from outta_context.storage.registry import StorageRegistry


def _size(n: float) -> str:
    return f"{n / 1_000_000:.1f} MB"


class UploadService:
    def __init__(
        self,
        storages: StorageRegistry,
        repo: LibraryRepository,
        settings: Settings,
        reporter: Reporter,
    ) -> None:
        self.storages = storages
        self.repo = repo
        self.settings = settings
        self.reporter = reporter

    def resolve_storage(self, name: str | None) -> str:
        chosen = name or self.settings.default_storage
        if not chosen:
            raise StorageNotConfigured("No storage chosen and no default storage is set.")
        self.storages.get(chosen)
        return chosen

    def upload_file(
        self,
        path: Path,
        storage: str | None = None,
        subfolder: str = "",
        kind: str = "file",
        ref: str = "",
    ) -> str:
        name = self.resolve_storage(storage)
        if not path.is_file():
            raise UploadFailed(f"{path} does not exist.", "Check the path or clip ID.")
        config = self.storages.get(name)
        backend = self.storages.backend(config)
        remote_key = f"{subfolder}/{path.name}" if subfolder else path.name
        previous = self.repo.find_upload(path, name, remote_key)
        if previous is not None:
            self.reporter.success(
                f"Already uploaded {path.name} to {name} ({previous.uploaded_at})"
            )
            return previous.remote_path
        size = path.stat().st_size
        self.reporter.step(f"Uploading {path.name} ({_size(size)}) to {name}")
        with self.reporter.progress(path.name, total=size) as task:
            where = backend.upload(path, subfolder, progress=task.update)
        self.repo.record_upload(kind, ref or str(path), path, name, remote_key)
        self.reporter.success(f"Uploaded to {where}")
        return where

    def upload_clip(self, clip: Clip, storage: str | None = None, subfolder: str = "") -> str:
        if not clip.file_path.exists():
            raise ClipFileMissing(f"The file for clip {clip.id} is missing: {clip.file_path}")
        return self.upload_file(clip.file_path, storage, subfolder, "clip", str(clip.id))

    def upload_episode(
        self, episode: Episode, clips: list[Clip], storage: str | None = None
    ) -> list[str]:
        if episode.output_path is None or not episode.output_path.exists():
            raise UploadFailed(
                f'"{episode.name}" has no built video yet.', "Build it with `outta episode build`."
            )
        folder = safe_filename(episode.name)
        uploaded = [self.upload_file(episode.output_path, storage, folder, "episode", episode.name)]
        for clip in clips:
            uploaded.append(self.upload_clip(clip, storage, f"{folder}/clips"))
        return uploaded
