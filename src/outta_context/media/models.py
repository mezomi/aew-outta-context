from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path


@dataclass(frozen=True)
class VideoInfo:
    video_id: str
    title: str
    duration: float | None
    webpage_url: str
    extractor: str


@dataclass(frozen=True)
class DownloadedClip:
    path: Path
    size_bytes: int


@dataclass(frozen=True)
class NormalizeTarget:
    width: int = 1920
    height: int = 1080
    fps: int = 30
    audio_rate: int = 48000
    video_codec: str = "libx264"
    audio_codec: str = "aac"
    crf: int = 20
    preset: str = "veryfast"

    def key(self) -> str:
        return (
            f"{self.width}x{self.height}@{self.fps}-{self.video_codec}-{self.crf}-{self.audio_rate}"
        )
