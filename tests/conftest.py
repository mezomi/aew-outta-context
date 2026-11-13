from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

import pytest

from outta_context.core.paths import AppPaths
from outta_context.core.reporter import RecordingReporter
from outta_context.library.episodes import EpisodeService
from outta_context.library.repository import LibraryRepository
from outta_context.media.ffmpeg import ProbeResult
from outta_context.media.models import DownloadedClip, NormalizeTarget, VideoInfo
from outta_context.settings.models import Settings

FIXTURES = Path(__file__).parent / "fixtures"


def pytest_addoption(parser: pytest.Parser) -> None:
    parser.addoption("--run-network", action="store_true", help="run tests that need internet")


def pytest_collection_modifyitems(config: pytest.Config, items: list[pytest.Item]) -> None:
    if config.getoption("--run-network"):
        return
    skip = pytest.mark.skip(reason="needs --run-network")
    for item in items:
        if "network" in item.keywords:
            item.add_marker(skip)


@pytest.fixture(autouse=True)
def app_home(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> Path:
    home = tmp_path / "apphome"
    monkeypatch.setenv("OUTTA_HOME", str(home))
    return home


@pytest.fixture
def paths(app_home: Path) -> AppPaths:
    p = AppPaths(app_home)
    p.ensure()
    return p


@pytest.fixture
def reporter() -> RecordingReporter:
    return RecordingReporter()


@pytest.fixture
def settings(tmp_path: Path) -> Settings:
    return Settings(clip_folder=tmp_path / "clips")


@pytest.fixture
def repo(paths: AppPaths) -> LibraryRepository:
    return LibraryRepository(paths.library_db)


@pytest.fixture
def episodes(repo: LibraryRepository, settings: Settings) -> EpisodeService:
    return EpisodeService(repo, settings.episode_length)


class FakePageFetcher:
    def __init__(self, pages: dict[str, str] | None = None) -> None:
        self.pages = pages or {}
        self.calls: list[str] = []

    def fetch(self, url: str) -> str:
        self.calls.append(url)
        return self.pages[url]


@pytest.fixture
def fake_fetcher() -> FakePageFetcher:
    return FakePageFetcher()


def fixture_page(name: str) -> str:
    return (FIXTURES / "pages" / name).read_text(encoding="utf-8")


@dataclass
class FakeDownloader:
    """Writes a small file instead of downloading."""

    duration: float | None = 600.0
    title: str = "AEW Dynamite highlights"
    fail_with: Exception | None = None
    calls: list[dict[str, Any]] = field(default_factory=list)

    def get_info(self, url: str, referer: str | None = None, detail: Any = None) -> VideoInfo:
        self.calls.append({"op": "info", "url": url, "referer": referer})
        return VideoInfo("vid", self.title, self.duration, url, "Fake")

    def download(
        self,
        url: str,
        output_stem: Path,
        time_range: Any,
        max_quality: int,
        referer: str | None = None,
        detail: Any = None,
        progress: Any = None,
    ) -> DownloadedClip:
        self.calls.append(
            {
                "op": "download",
                "url": url,
                "stem": output_stem,
                "range": time_range,
                "quality": max_quality,
                "referer": referer,
            }
        )
        if self.fail_with:
            raise self.fail_with
        if progress:
            progress(50, 100)
            progress(100, 100)
        path = output_stem.with_name(output_stem.name + ".mp4")
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(b"x" * 100)
        return DownloadedClip(path, 100)


@dataclass
class FakeFfmpeg:
    """Pretends to run ffmpeg; durations come from a dict or default."""

    is_available: bool = True
    durations: dict[str, float] = field(default_factory=dict)
    default_duration: float = 20.0
    normalized: list[tuple[Path, Path]] = field(default_factory=list)
    concats: list[tuple[list[Path], Path]] = field(default_factory=list)

    def available(self) -> bool:
        return self.is_available

    def version(self) -> str | None:
        return "ffmpeg version 7.1 Copyright" if self.is_available else None

    def ensure_available(self) -> None:
        from outta_context.core.errors import FfmpegMissing

        if not self.is_available:
            raise FfmpegMissing("ffmpeg not found")

    def probe(self, path: Path) -> ProbeResult:
        if path.name in self.durations:
            d = self.durations[path.name]
        elif path in [dst for _, dst in self.concats]:
            d = sum(self.durations.get(s.name, self.default_duration) for s in self.concats[-1][0])
        else:
            d = self.default_duration
        return ProbeResult(d, 1920, 1080, 30.0, True)

    def normalize(self, src: Path, dst: Path, target: NormalizeTarget) -> None:
        self.normalized.append((src, dst))
        dst.parent.mkdir(parents=True, exist_ok=True)
        dst.write_bytes(b"n")

    def concat(self, paths: list[Path], dst: Path, workdir: Path) -> None:
        self.concats.append((list(paths), dst))
        dst.parent.mkdir(parents=True, exist_ok=True)
        dst.write_bytes(b"episode")


class MemoryKeyring:
    """In-memory keyring backend for tests."""

    def __init__(self) -> None:
        self.data: dict[tuple[str, str], str] = {}

    def get_password(self, service: str, key: str) -> str | None:
        return self.data.get((service, key))

    def set_password(self, service: str, key: str, value: str) -> None:
        self.data[(service, key)] = value

    def delete_password(self, service: str, key: str) -> None:
        self.data.pop((service, key), None)

    def get_keyring(self) -> object:
        return self


@pytest.fixture
def memory_keyring() -> MemoryKeyring:
    return MemoryKeyring()
