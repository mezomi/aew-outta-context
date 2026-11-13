"""Real ffmpeg + real yt-dlp against generated test videos. Run: pytest -m integration"""

from __future__ import annotations

import contextlib
import functools
import http.server
import shutil
import subprocess
import threading
from collections.abc import Iterator
from pathlib import Path

import pytest

from outta_context.core.reporter import RecordingReporter
from outta_context.core.timerange import TimeRange
from outta_context.library.episodes import EpisodeService
from outta_context.library.repository import LibraryRepository
from outta_context.media.downloader import ClipDownloader
from outta_context.media.ffmpeg import Ffmpeg
from outta_context.media.models import NormalizeTarget
from outta_context.settings.models import Settings
from outta_context.workflows.build_episode import BuildEpisodeWorkflow

pytestmark = [
    pytest.mark.integration,
    pytest.mark.skipif(shutil.which("ffmpeg") is None, reason="needs ffmpeg"),
]

SMALL = NormalizeTarget(width=640, height=360, fps=30, preset="ultrafast", crf=30)


def make_video(path: Path, seconds: float, size: str, rate: int, audio: bool = True) -> Path:
    cmd = [
        "ffmpeg",
        "-loglevel",
        "error",
        "-y",
        "-f",
        "lavfi",
        "-i",
        f"testsrc2=size={size}:rate={rate}:duration={seconds}",
    ]
    if audio:
        cmd += ["-f", "lavfi", "-i", f"sine=frequency=440:duration={seconds}:sample_rate=44100"]
    cmd += ["-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", "-g", str(rate * 2)]
    if audio:
        cmd += ["-c:a", "aac", "-shortest"]
    cmd += [str(path)]
    subprocess.run(cmd, check=True)
    return path


def test_build_episode_from_mixed_clips(tmp_path: Path) -> None:
    clips_dir = tmp_path / "clips"
    clips_dir.mkdir()
    sources = [
        ("Dailymotion-ish 720p25", 3, "1280x720", 25, True),
        ("YouTube-ish 1080p60", 2, "1920x1080", 60, True),
        ("Vertical silent", 2, "360x640", 30, False),
    ]
    repo = LibraryRepository(tmp_path / "library.db")
    episodes = EpisodeService(repo, 600)
    for title, secs, size, rate, audio in sources:
        path = make_video(clips_dir / f"{title}.mp4", secs, size, rate, audio)
        episodes.add_clip(repo.add_clip(title, "test", "u", 0, secs, secs, path))
    ffmpeg = Ffmpeg()
    settings = Settings(clip_folder=clips_dir)
    reporter = RecordingReporter()
    wf = BuildEpisodeWorkflow(
        episodes, ffmpeg, settings, tmp_path / "cache", reporter, target=SMALL
    )
    result = wf.run()
    info = ffmpeg.probe(result.output)
    assert info.duration == pytest.approx(7, abs=0.3)
    assert (info.width, info.height) == (640, 360)
    assert info.fps == pytest.approx(30, abs=0.5)
    assert info.has_audio
    assert result.next_episode is not None
    # Rebuild uses cached conversions
    wf.run("Episode 1")
    assert sum("using converted copy" in s for s in reporter.texts("step")) == 3


class RangeHandler(http.server.SimpleHTTPRequestHandler):
    """Static files with HTTP Range support (ffmpeg seeks like a real video CDN)."""

    def log_message(self, *args: object) -> None:
        return None

    def do_GET(self) -> None:
        path = Path(self.translate_path(self.path))
        header = self.headers.get("Range")
        if not header or not path.is_file():
            super().do_GET()
            return
        size = path.stat().st_size
        start_text, _, end_text = header.removeprefix("bytes=").partition("-")
        start = int(start_text or 0)
        end = min(int(end_text) if end_text else size - 1, size - 1)
        self.send_response(206)
        self.send_header("Content-Type", "video/mp4")
        self.send_header("Accept-Ranges", "bytes")
        self.send_header("Content-Range", f"bytes {start}-{end}/{size}")
        self.send_header("Content-Length", str(end - start + 1))
        self.end_headers()
        with path.open("rb") as f:
            f.seek(start)
            with contextlib.suppress(BrokenPipeError, ConnectionResetError):
                self.wfile.write(f.read(end - start + 1))


@pytest.fixture
def http_dir(tmp_path: Path) -> Iterator[tuple[str, Path]]:
    root = tmp_path / "www"
    root.mkdir()
    handler = functools.partial(RangeHandler, directory=str(root))
    server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), handler)
    server.handle_error = lambda *a: None  # type: ignore[method-assign]  # client hang-ups
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    yield f"http://127.0.0.1:{server.server_address[1]}", root
    server.shutdown()


def test_real_ytdlp_cuts_exact_range(
    http_dir: tuple[str, Path], tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    for var in ("HTTP_PROXY", "HTTPS_PROXY", "http_proxy", "https_proxy", "ALL_PROXY", "all_proxy"):
        monkeypatch.delenv(var, raising=False)
    base, root = http_dir
    make_video(root / "source.mp4", 20, "1280x720", 30)
    downloader = ClipDownloader()
    url = f"{base}/source.mp4"
    info = downloader.get_info(url)
    # yt-dlp's generic extractor doesn't report a length for a bare .mp4 (real sites do)
    assert info.duration is None or info.duration == pytest.approx(20, abs=0.2)
    progress: list[float] = []
    clip = downloader.download(
        url,
        tmp_path / "out" / "Table spot",
        TimeRange(5.0, 9.5),
        1080,
        progress=lambda done, total: progress.append(done),
    )
    assert clip.path.name == "Table spot.mp4"
    length = Ffmpeg().probe(clip.path).duration
    assert length == pytest.approx(4.5, abs=0.25)
