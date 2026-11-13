"""User settings and their defaults."""

from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path

DEFAULT_EPISODE_LENGTH = 600.0
DEFAULT_MAX_QUALITY = 1080
DEFAULT_MAX_CLIP_LENGTH = 45.0


def default_clip_folder() -> Path:
    return Path.home() / "Videos" / "Outta Context"


@dataclass
class Settings:
    clip_folder: Path = field(default_factory=default_clip_folder)
    episode_length: float = DEFAULT_EPISODE_LENGTH
    max_quality: int = DEFAULT_MAX_QUALITY
    max_clip_length: float | None = DEFAULT_MAX_CLIP_LENGTH  # None = no cap
    default_storage: str | None = None

    @property
    def episodes_folder(self) -> Path:
        return self.clip_folder / "Episodes"
