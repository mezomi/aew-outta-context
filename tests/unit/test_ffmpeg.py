import json
import subprocess
from pathlib import Path
from typing import Any

import pytest

from outta_context.core.errors import FfmpegFailed, FfmpegMissing
from outta_context.media.ffmpeg import Ffmpeg, _parse_rate
from outta_context.media.models import NormalizeTarget


class Runner:
    def __init__(self, stdout: str = "", returncode: int = 0, stderr: str = "") -> None:
        self.calls: list[list[str]] = []
        self.stdout, self.returncode, self.stderr = stdout, returncode, stderr

    def __call__(self, cmd: list[str], **kw: Any) -> subprocess.CompletedProcess[str]:
        self.calls.append(cmd)
        if cmd[-1].endswith(".mp4") and "-i" in cmd:
            Path(cmd[-1]).write_bytes(b"out")
        return subprocess.CompletedProcess(cmd, self.returncode, self.stdout, self.stderr)


def found(name: str) -> str:
    return "/usr/bin/" + name


def test_missing_ffmpeg() -> None:
    ff = Ffmpeg(runner=Runner(), which=lambda n: None)
    assert not ff.available()
    with pytest.raises(FfmpegMissing) as exc:
        ff.ensure_available()
    assert "winget install ffmpeg" in exc.value.hint
    assert ff.version() is None


def test_probe_parses_json() -> None:
    data = {
        "format": {"duration": "20.04"},
        "streams": [
            {"codec_type": "video", "width": 1280, "height": 720, "avg_frame_rate": "30000/1001"},
            {"codec_type": "audio"},
        ],
    }
    runner = Runner(json.dumps(data))
    info = Ffmpeg(runner=runner, which=found).probe(Path("a.mp4"))
    assert info.duration == pytest.approx(20.04)
    assert (info.width, info.height, info.has_audio) == (1280, 720, True)
    assert info.fps == pytest.approx(29.97, 0.01)
    assert runner.calls[0][0] == "ffprobe"


def test_parse_rate() -> None:
    assert _parse_rate("0/0") is None
    assert _parse_rate(None) is None
    assert _parse_rate("25") == 25
    assert _parse_rate("1/0") is None


def test_normalize_command() -> None:
    ff = Ffmpeg(runner=Runner(), which=found)
    cmd = ff.normalize_command(Path("in.mp4"), Path("out.mp4"), NormalizeTarget())
    vf = cmd[cmd.index("-vf") + 1]
    assert "scale=1920:1080:force_original_aspect_ratio=decrease" in vf
    assert "pad=1920:1080" in vf
    assert "fps=30" in vf
    assert cmd[cmd.index("-c:v") + 1] == "libx264"
    assert cmd[cmd.index("-ar") + 1] == "48000"
    assert "anullsrc" not in " ".join(cmd)
    silent = ff.normalize_command(
        Path("in.mp4"), Path("out.mp4"), NormalizeTarget(), has_audio=False
    )
    assert "anullsrc=r=48000:cl=stereo" in silent
    assert "-shortest" in silent
    assert silent[silent.index("-map", silent.index("-map") + 1) + 1] == "1:a:0"


def test_normalize_runs_probe_then_ffmpeg(tmp_path: Path) -> None:
    runner = Runner(json.dumps({"format": {"duration": "5"}, "streams": [{"codec_type": "video"}]}))
    logged: list[str] = []
    ff = Ffmpeg(runner=runner, which=found, detail=logged.append)
    ff.normalize(tmp_path / "in.mp4", tmp_path / "n" / "out.mp4", NormalizeTarget())
    assert [c[0] for c in runner.calls] == ["ffprobe", "ffmpeg"]
    assert "anullsrc" in " ".join(runner.calls[1])  # no audio stream → silent track added
    assert (tmp_path / "n" / "out.mp4").exists()
    assert logged[0].startswith("$ ffprobe")


def test_concat(tmp_path: Path) -> None:
    runner = Runner()
    ff = Ffmpeg(runner=runner, which=found)
    a, b = tmp_path / "a.mp4", tmp_path / "it's.mp4"
    ff.concat([a, b], tmp_path / "ep" / "Episode 1.mp4", tmp_path / "work")
    listing = (tmp_path / "work" / "concat.txt").read_text()
    assert f"file '{a.resolve().as_posix()}'" in listing
    assert "it'\\''s.mp4" in listing
    cmd = runner.calls[0]
    assert cmd[cmd.index("-f") + 1] == "concat"
    assert cmd[cmd.index("-c") + 1] == "copy"
    assert (tmp_path / "ep" / "Episode 1.mp4").exists()


def test_failure_raises_with_stderr_tail() -> None:
    ff = Ffmpeg(runner=Runner(returncode=1, stderr="line1\nInvalid data found"), which=found)
    with pytest.raises(FfmpegFailed, match="Invalid data found"):
        ff.probe(Path("x.mp4"))


def test_version_and_file_not_found() -> None:
    ff = Ffmpeg(runner=Runner("ffmpeg version 7.1 Copyright"), which=found)
    assert ff.version() == "ffmpeg version 7.1 Copyright"

    def missing(cmd: list[str], **kw: Any) -> Any:
        raise FileNotFoundError

    with pytest.raises(FfmpegMissing):
        Ffmpeg(runner=missing, which=found).probe(Path("x"))
