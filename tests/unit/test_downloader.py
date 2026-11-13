from pathlib import Path
from typing import Any, ClassVar

import pytest

from outta_context.core.errors import DownloadFailed
from outta_context.core.timerange import TimeRange
from outta_context.media.downloader import ClipDownloader, format_selector, translate_error


class FakeYDL:
    instances: ClassVar[list["FakeYDL"]] = []

    def __init__(
        self,
        options: dict[str, Any],
        info: Any = None,
        error: Exception | None = None,
        code: int = 0,
        write: str | None = ".mp4",
    ) -> None:
        self.options = options
        self.info = info
        self.error = error
        self.code = code
        self.write = write
        FakeYDL.instances.append(self)

    def __enter__(self) -> "FakeYDL":
        return self

    def __exit__(self, *a: object) -> None:
        return None

    def extract_info(self, url: str, download: bool = False) -> Any:
        if self.error:
            raise self.error
        return self.info

    def download(self, urls: list[str]) -> int:
        if self.error:
            raise self.error
        for hook in self.options.get("progress_hooks", []):
            hook({"status": "downloading", "downloaded_bytes": 50, "total_bytes": 100})
            hook({"status": "finished", "downloaded_bytes": 100, "total_bytes_estimate": 100})
        if self.write:
            stem = self.options["outtmpl"]["default"].replace(".%(ext)s", "")
            Path(stem + self.write).write_bytes(b"video")
        if self.code:
            self.options["logger"].error("ERROR: Private video")
        return self.code


def factory(**kw: Any) -> Any:
    return lambda options: FakeYDL(options, **kw)


def test_format_selector() -> None:
    assert "height<=720" in format_selector(720)
    assert format_selector(1080).endswith("/bv*+ba/b")


def test_download_options(tmp_path: Path) -> None:
    d = ClipDownloader(factory(), ffmpeg_location="C:/ffmpeg/bin")
    opts = d.build_download_options(
        tmp_path / "Table spot",
        TimeRange(45, 65),
        1080,
        referer="https://page",
        progress=lambda a, b: None,
    )
    assert opts["format"] == format_selector(1080)
    assert opts["outtmpl"]["default"] == f"{tmp_path / 'Table spot'}.%(ext)s"
    assert opts["http_headers"] == {"Referer": "https://page"}
    assert opts["force_keyframes_at_cuts"] is True
    assert opts["merge_output_format"] == "mp4"
    assert opts["noplaylist"] is True
    assert opts["ffmpeg_location"] == "C:/ffmpeg/bin"
    ranges = list(opts["download_ranges"]({}, None))
    assert ranges[0]["start_time"] == 45
    assert ranges[0]["end_time"] == 65
    assert "http_headers" not in d.build_download_options(tmp_path / "x", TimeRange(0, 1), 720)


def test_get_info() -> None:
    raw = {
        "id": "abc",
        "title": "AEW",
        "duration": 872,
        "webpage_url": "u",
        "extractor_key": "Youtube",
    }
    info = ClipDownloader(factory(info=raw)).get_info("u", referer="https://p")
    assert (info.title, info.duration, info.extractor) == ("AEW", 872.0, "Youtube")
    assert FakeYDL.instances[-1].options["skip_download"] is True
    assert ClipDownloader(factory(info={"id": "x"})).get_info("u").duration is None
    with pytest.raises(DownloadFailed, match="playlist"):
        ClipDownloader(factory(info={"_type": "playlist"})).get_info("u")
    with pytest.raises(DownloadFailed, match="no information"):
        ClipDownloader(factory(info=None)).get_info("u")
    with pytest.raises(DownloadFailed, match="not available"):
        ClipDownloader(
            factory(error=RuntimeError("ERROR: Video unavailable. This video is private"))
        ).get_info("u")


def test_download_writes_and_reports_progress(tmp_path: Path) -> None:
    updates: list[tuple[float, float | None]] = []
    clip = ClipDownloader(factory()).download(
        "u",
        tmp_path / "sub" / "Clip",
        TimeRange(0, 5),
        1080,
        progress=lambda a, b: updates.append((a, b)),
    )
    assert clip.path == tmp_path / "sub" / "Clip.mp4"
    assert clip.size_bytes == 5
    assert updates == [(50, 100), (100, 100)]


def test_download_other_extension_and_failures(tmp_path: Path) -> None:
    clip = ClipDownloader(factory(write=".mkv")).download(
        "u", tmp_path / "C", TimeRange(0, 5), 1080
    )
    assert clip.path.suffix == ".mkv"
    with pytest.raises(DownloadFailed, match="no video file"):
        ClipDownloader(factory(write=None)).download("u", tmp_path / "D", TimeRange(0, 5), 1080)
    with pytest.raises(DownloadFailed, match="not available"):
        ClipDownloader(factory(code=1, write=None)).download(
            "u", tmp_path / "E", TimeRange(0, 5), 1080
        )
    with pytest.raises(DownloadFailed, match="Network"):
        ClipDownloader(
            factory(error=RuntimeError("Unable to download webpage: timed out"))
        ).download("u", tmp_path / "F", TimeRange(0, 5), 1080)


@pytest.mark.parametrize(
    ("message", "expected"),
    [
        (
            "ERROR: The uploader has not made this video available in your country (geo restricted)",
            "blocked",
        ),
        ("ERROR: Private video", "not available"),
        ("Sign in to confirm you're not a bot", "login"),
        ("ffmpeg not found. Please install", "needs ffmpeg"),
        ("ERROR: ffmpeg exited with code 183", "could not cut"),
        ("connection reset", "Network"),
        ("weird thing", "Download failed"),
    ],
)
def test_translate_error(message: str, expected: str) -> None:
    err = translate_error(message)
    assert expected in err.message
    assert err.hint
    assert not err.message.startswith("Download failed: ERROR:")
    assert "please report" not in err.message


def test_logger_forwards_details() -> None:
    from outta_context.media.downloader import _Logger

    seen: list[str] = []
    log = _Logger(seen.append)
    log.debug("[debug] hidden")
    log.debug("[youtube] shown")
    log.info("info")
    log.warning("careful")
    log.error("bad")
    assert seen == ["[youtube] shown", "info", "yt-dlp warning: careful"]
    assert log.errors == ["bad"]
