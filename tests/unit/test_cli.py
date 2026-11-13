import json
import re
from pathlib import Path

import pytest
from rich.console import Console
from typer.testing import CliRunner

from outta_context import __version__
from outta_context.cli import context
from outta_context.cli.app import app
from outta_context.core.reporter import Verbosity
from outta_context.storage.credentials import CredentialStore
from tests.conftest import FakeDownloader, FakeFfmpeg, FakePageFetcher, MemoryKeyring, fixture_page

runner = CliRunner()


@pytest.fixture
def fakes(monkeypatch: pytest.MonkeyPatch, tmp_path: Path) -> dict[str, object]:
    made: dict[str, object] = {
        "downloader": FakeDownloader(),
        "ffmpeg": FakeFfmpeg(),
        "fetcher": FakePageFetcher(
            {
                "https://news/a": fixture_page("dailymotion_article.html"),
                "https://news/empty": fixture_page("no_videos.html"),
            }
        ),
    }

    def factory(level: Verbosity) -> context.AppServices:
        s = context.AppServices(level, Console(width=200, force_terminal=False, highlight=False))
        s.__dict__.update(made)  # override cached_property values
        s.__dict__["credentials"] = CredentialStore(MemoryKeyring())
        return s

    monkeypatch.setattr(context, "services_factory", factory)
    result = runner.invoke(app, ["config", "set-destination", str(tmp_path / "clips")])
    assert result.exit_code == 0, result.output
    return made


def run(*args: str) -> tuple[int, str]:
    result = runner.invoke(app, list(args))
    out = result.output + (result.stderr if result.stderr_bytes else "")
    return result.exit_code, re.sub(r"\x1b\[[0-9;]*m", "", out)  # CI forces color


def test_version_and_help() -> None:
    code, out = run("--version")
    assert code == 0
    assert __version__ in out
    code, out = run()
    assert "clip" in out
    assert "scrape" in out


def test_clip_requires_title_and_range(fakes: dict[str, object]) -> None:
    code, out = run("clip", "https://youtu.be/dQw4w9WgXcQ", "--range", "0-5")
    assert code == 2
    assert "--title" in out
    code, out = run("clip", "https://youtu.be/dQw4w9WgXcQ", "--title", "x")
    assert code == 2
    assert "--range" in out


def test_clip_end_to_end(fakes: dict[str, object], tmp_path: Path) -> None:
    code, out = run(
        "clip", "https://youtu.be/dQw4w9WgXcQ", "--range", "0:45-1:05", "--title", "Table spot"
    )
    assert code == 0, out
    assert "Detected source: YouTube" in out
    assert "Table spot.mp4" in out
    assert 'Added to "Episode 1": 0:20 of 10:00 (9:40 left)' in out
    code, out = run("episode", "status")
    assert "Table spot" in out
    assert "9:40 left" in out
    code, out = run("episode", "status", "--json")
    assert json.loads(out)["clips"][0]["title"] == "Table spot"


def test_errors_print_message_and_hint(fakes: dict[str, object]) -> None:
    code, out = run(
        "clip", "https://youtu.be/dQw4w9WgXcQ", "--range", "1:30-11:52", "--title", "typo"
    )
    assert code == 1
    assert "error:" in out
    assert "over the 45s clip cap" in out
    assert "hint:" in out
    code, out = run("clip", "https://youtu.be/dQw4w9WgXcQ", "--range", "1:52-1:30", "--title", "x")
    assert code == 1
    assert "must be after" in out
    code, out = run("-q", "clip", "https://vimeo.com/1", "--range", "0-5", "--title", "x")
    assert code == 1
    assert "Not a Dailymotion or YouTube" in out
    code, out = run(
        "clip",
        "https://youtu.be/dQw4w9WgXcQ",
        "-r",
        "0-5",
        "-t",
        "x",
        "--episode",
        "a",
        "--no-episode",
    )
    assert code == 2


def test_unexpected_errors_are_caught(fakes: dict[str, object]) -> None:
    fakes["downloader"].fail_with = RuntimeError("kaboom")  # type: ignore[attr-defined]
    code, out = run("clip", "https://youtu.be/dQw4w9WgXcQ", "-r", "0-5", "-t", "x")
    assert code == 1
    assert "Unexpected problem: RuntimeError: kaboom" in out
    assert "Traceback" not in out
    code, out = run("--verbose", "clip", "https://youtu.be/dQw4w9WgXcQ", "-r", "0-5", "-t", "y")
    assert "Traceback" in out


def test_scrape(fakes: dict[str, object]) -> None:
    code, out = run("scrape", "dailymotion", "https://news/a")
    assert code == 0, out
    assert "Found 5 videos" in out
    assert "https://www.dailymotion.com/video/k1ojSwOaH7psOtMbpGS" in out
    assert "(geo player embed)" in out
    code, out = run("scrape", "dailymotion", "https://news/a", "--json")
    assert json.loads(out)[0]["page_url"] == "https://news/a"
    code, out = run("scrape", "youtube", "https://news/empty")
    assert code == 1
    assert "JavaScript" in out


def test_prefixes(fakes: dict[str, object]) -> None:
    code, out = run("scrape", "prefixes", "add", "dailymotion", "https://players.example-cdn.com/")
    assert code == 0, out
    code, out = run("scrape", "prefixes", "list", "dailymotion")
    assert "https://players.example-cdn.com/" in out
    assert "geo player embed" in out
    code, out = run("scrape", "prefixes", "add", "dailymotion", "https://players.example-cdn.com/")
    assert code == 1
    code, out = run("scrape", "prefixes", "add", "vimeo", "https://x/")
    assert code == 1
    assert "Sites you can use" in out
    code, out = run(
        "scrape", "prefixes", "remove", "dailymotion", "https://players.example-cdn.com/"
    )
    assert code == 0
    code, out = run("scrape", "prefixes", "list")
    assert "YouTube" in out


def test_episode_commands(fakes: dict[str, object]) -> None:
    for i, t in enumerate(["a", "b", "c"]):
        code, out = run("clip", "https://youtu.be/dQw4w9WgXcQ", "-r", f"{i}-{i + 5}", "-t", t)
        assert code == 0, out
    code, out = run("episode", "move", "3", "1")
    assert code == 0, out
    code, out = run("episode", "status", "--json")
    assert [c["title"] for c in json.loads(out)["clips"]] == ["c", "a", "b"]
    code, out = run("episode", "remove", "1")
    assert "file kept" in out
    code, out = run("episode", "move", "2", "9")
    assert code == 1
    code, out = run("episode", "build")
    assert code == 0, out
    assert 'Opened "Episode 2"' in out
    code, out = run("episode", "list")
    assert "Episode 1" in out
    assert "built" in out
    assert "current" in out
    code, out = run("episode", "new", "Special")
    assert code == 0
    code, out = run("episode", "new", "special")
    assert code == 1
    code, out = run("episode", "list", "--json")
    assert [e["name"] for e in json.loads(out)] == ["Episode 1", "Episode 2", "Special"]
    code, out = run("episode", "build")
    assert code == 1
    assert "has no clips" in out


def test_config(fakes: dict[str, object]) -> None:
    code, out = run("config", "set", "episode-length", "8:00")
    assert code == 0
    assert "8:00" in out
    code, out = run("config", "set", "max-clip-length", "1:00")
    assert "1:00" in out
    code, out = run("config", "set", "bogus", "1")
    assert code == 1
    code, out = run("config", "show")
    assert "8:00" in out
    assert "max-clip-length" in out
    assert "Data folder" in out


def test_storage_and_upload_local(fakes: dict[str, object], tmp_path: Path) -> None:
    nas = tmp_path / "nas"
    nas.mkdir()
    code, out = run("storage", "add", "nas", "--provider", "local", "--folder", str(nas))
    assert code == 0, out
    assert "nas works" in out
    assert "default storage" in out
    code, out = run("storage", "list")
    assert "nas" in out
    assert "(default)" in out
    code, out = run("storage", "add", "nas", "--provider", "local", "--folder", str(nas))
    assert code == 1
    code, out = run("storage", "add", "x", "--provider", "ftp")
    assert code == 1
    code, out = run("storage", "test", "nas")
    assert code == 0
    code, out = run("clip", "https://youtu.be/dQw4w9WgXcQ", "-r", "0-5", "-t", "Up", "--upload")
    assert code == 0, out
    assert (nas / "Up.mp4").exists()
    code, out = run("upload", "1")
    assert "Already uploaded" in out
    f = tmp_path / "loose.mp4"
    f.write_bytes(b"z")
    code, out = run("upload", str(f), "--to", "nas")
    assert (nas / "loose.mp4").exists()
    code, out = run("episode", "build", "--upload")
    assert code == 0, out
    assert (nas / "Episode 1" / "Episode 1.mp4").exists()
    code, out = run("upload", "Episode 1")
    assert "Already uploaded" in out
    code, out = run("storage", "default", "nope")
    assert code == 1
    code, out = run("storage", "remove", "nas")
    assert code == 0
    code, out = run("storage", "list")
    assert "No storage yet" in out


def test_doctor_runs(fakes: dict[str, object]) -> None:
    code, out = run("doctor", "--no-storage")
    assert "ffmpeg" in out
    assert "yt-dlp" in out
    assert code in (0, 1)
