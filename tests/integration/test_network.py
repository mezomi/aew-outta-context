"""Real sites. Skipped unless: pytest -m network --run-network

Run occasionally to catch site changes. Set OUTTA_TEST_YT / OUTTA_TEST_DM to use other videos.
"""

from __future__ import annotations

import os
import shutil
from pathlib import Path

import pytest

from outta_context.core.timerange import TimeRange
from outta_context.media.downloader import ClipDownloader
from outta_context.media.ffmpeg import Ffmpeg
from outta_context.sources.registry import SourceRegistry

pytestmark = [
    pytest.mark.network,
    pytest.mark.skipif(shutil.which("ffmpeg") is None, reason="needs ffmpeg"),
]

VIDEOS = {
    "youtube": os.environ.get("OUTTA_TEST_YT", "https://www.youtube.com/watch?v=jNQXAC9IVRw"),
    "dailymotion": os.environ.get("OUTTA_TEST_DM", "https://www.dailymotion.com/video/x8j2v1c"),
}


@pytest.mark.parametrize("site", sorted(VIDEOS))
def test_real_clip(site: str, tmp_path: Path) -> None:
    url = SourceRegistry().detect(VIDEOS[site]).canonical_url(VIDEOS[site])
    downloader = ClipDownloader()
    info = downloader.get_info(url)
    assert info.duration
    assert info.duration > 5
    clip = downloader.download(url, tmp_path / "clip", TimeRange(1, 4), 480)
    assert Ffmpeg().probe(clip.path).duration == pytest.approx(3, abs=0.6)
