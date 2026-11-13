from pathlib import Path

import pytest
from rich.console import Console

from outta_context.core.errors import DuplicateTitle, OuttaError
from outta_context.core.paths import AppPaths
from outta_context.core.reporter import ConsoleReporter, RecordingReporter, Verbosity


def test_error_has_default_and_custom_hint() -> None:
    assert DuplicateTitle("x").hint.startswith("Pick a different")
    assert DuplicateTitle("x", "custom").hint == "custom"
    assert str(OuttaError("boom")) == "boom"
    assert OuttaError("boom").hint == ""


def _console() -> Console:
    return Console(record=True, width=120, force_terminal=False)


def test_console_reporter_levels() -> None:
    for level, shown in [
        (Verbosity.QUIET, {"ok", "careful"}),
        (Verbosity.NORMAL, {"doing", "ok", "careful", "plain"}),
        (Verbosity.VERBOSE, {"doing", "ok", "careful", "plain", "deep"}),
    ]:
        console = _console()
        r = ConsoleReporter(console, level)
        r.step("doing")
        r.success("ok")
        r.warn("careful")
        r.detail("deep")
        r.info("plain")
        with r.progress("bar", total=10) as task:
            task.update(5)
            task.update(10, 10)
        out = console.export_text()
        for word in ("doing", "deep"):
            assert (word in out) == (word in shown), (level, word)
        assert "ok" in out
        assert "careful" in out


def test_console_reporter_escapes_markup() -> None:
    console = _console()
    ConsoleReporter(console).success("[bold]not markup[/]")
    assert "[bold]not markup[/]" in console.export_text()


def test_recording_reporter() -> None:
    r = RecordingReporter()
    r.step("a")
    r.success("b")
    with r.progress("p", 3) as t:
        t.update(1)
    assert r.kinds() == ["step", "success", "progress"]
    assert r.texts("success") == ["b"]
    assert r.tasks[0].updates == [(1, None)]


def test_app_paths(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("OUTTA_HOME", str(tmp_path / "h"))
    p = AppPaths.default()
    assert p.data_dir == tmp_path / "h"
    assert p.settings_file.name == "settings.toml"
    assert p.library_db.name == "library.db"
    assert p.prefixes_file.name == "prefixes.toml"
    assert p.storage_file.name == "storage.toml"
    assert p.rclone_config.name == "rclone.conf"
    p.ensure()
    assert p.data_dir.is_dir()
    monkeypatch.delenv("OUTTA_HOME")
    assert "outta-context" in str(AppPaths.default().data_dir)
