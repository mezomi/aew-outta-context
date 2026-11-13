"""StorageBackend: somewhere clips and episodes are uploaded to."""

from __future__ import annotations

import abc
from collections.abc import Callable
from dataclasses import dataclass, field
from pathlib import Path

ProgressCallback = Callable[[float, float | None], None]


@dataclass(frozen=True)
class StorageConfig:
    name: str
    provider: str  # gdrive | onedrive | dropbox | b2 | local
    folder: str = ""  # remote folder (or a local path for provider=local)
    options: dict[str, str] = field(default_factory=dict)  # non-secret, e.g. b2 account ID

    def as_dict(self) -> dict[str, object]:
        data: dict[str, object] = {"provider": self.provider, "folder": self.folder}
        if self.options:
            data["options"] = dict(self.options)
        return data


def join_remote(*parts: str) -> str:
    return "/".join(p.strip("/") for p in parts if p and p.strip("/"))


class StorageBackend(abc.ABC):
    def __init__(self, config: StorageConfig) -> None:
        self.config = config

    @abc.abstractmethod
    def upload(
        self, local_path: Path, subfolder: str = "", progress: ProgressCallback | None = None
    ) -> str:
        """Upload a file into <folder>/<subfolder>/ and return where it went."""

    @abc.abstractmethod
    def test(self) -> None:
        """Raise StorageAuthFailed (or similar) if the storage can't be written to."""

    def remove(self) -> None:  # noqa: B027 - optional hook
        """Clean up provider state (logins, remotes) when the storage is removed."""

    def describe(self) -> str:
        return self.config.folder
