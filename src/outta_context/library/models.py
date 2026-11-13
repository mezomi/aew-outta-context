from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path


@dataclass(frozen=True)
class Clip:
    id: int
    title: str
    source: str
    source_url: str
    start: float
    end: float
    duration: float
    file_path: Path
    page_url: str | None
    created_at: str
    episode_id: int | None = None
    position: int | None = None


@dataclass(frozen=True)
class Episode:
    id: int
    name: str
    target_length: float
    state: str  # "open" or "built"
    output_path: Path | None
    created_at: str
    built_at: str | None

    @property
    def is_built(self) -> bool:
        return self.state == "built"


@dataclass(frozen=True)
class Upload:
    id: int
    kind: str  # clip | episode | file
    ref: str
    local_path: Path
    storage: str
    remote_path: str
    size: int
    uploaded_at: str


@dataclass(frozen=True)
class EpisodeStatus:
    episode: Episode
    clips: list[Clip] = field(default_factory=list)

    @property
    def total(self) -> float:
        return sum(c.duration for c in self.clips)

    @property
    def remaining(self) -> float:
        return max(0.0, self.episode.target_length - self.total)

    @property
    def ready(self) -> bool:
        return self.total >= self.episode.target_length
