"""Upload to another folder: an external drive, a NAS share or a synced folder."""

from __future__ import annotations

import shutil
import uuid
from pathlib import Path

from outta_context.core.errors import StorageAuthFailed, UploadFailed
from outta_context.storage.base import ProgressCallback, StorageBackend

_CHUNK = 4 * 1024 * 1024


class LocalBackend(StorageBackend):
    @property
    def root(self) -> Path:
        return Path(self.config.folder).expanduser()

    def upload(
        self, local_path: Path, subfolder: str = "", progress: ProgressCallback | None = None
    ) -> str:
        if not self.root.is_dir():
            raise UploadFailed(
                f"Storage folder {self.root} is not available.",
                "Plug in the drive or connect the share, then try again.",
            )
        target_dir = self.root / subfolder if subfolder else self.root
        target = target_dir / local_path.name
        total = local_path.stat().st_size
        try:
            target_dir.mkdir(parents=True, exist_ok=True)
            tmp = target.with_name(target.name + ".part")
            done = 0
            with local_path.open("rb") as src, tmp.open("wb") as dst:
                while chunk := src.read(_CHUNK):
                    dst.write(chunk)
                    done += len(chunk)
                    if progress:
                        progress(done, total)
            shutil.copystat(local_path, tmp)
            tmp.replace(target)
        except OSError as exc:
            raise UploadFailed(f"Could not copy to {target}: {exc}") from exc
        return str(target)

    def test(self) -> None:
        if not self.root.is_dir():
            raise StorageAuthFailed(
                f"Folder {self.root} does not exist or is not connected.",
                "Create the folder or connect the drive.",
            )
        probe = self.root / f".outta-test-{uuid.uuid4().hex[:8]}"
        try:
            probe.write_text("ok", encoding="utf-8")
            probe.unlink()
        except OSError as exc:
            raise StorageAuthFailed(
                f"Can't write to {self.root}: {exc}", "Check permissions."
            ) from exc
