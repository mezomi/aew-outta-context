from pathlib import Path

import pytest

from outta_context.core.errors import SettingsError
from outta_context.settings.models import Settings
from outta_context.settings.store import SettingsStore, as_display, parse_value


def test_defaults(tmp_path: Path) -> None:
    s = SettingsStore(tmp_path / "settings.toml").load()
    assert s.episode_length == 600
    assert s.max_quality == 1080
    assert s.default_storage is None
    assert s.episodes_folder == s.clip_folder / "Episodes"


def test_round_trip(tmp_path: Path) -> None:
    store = SettingsStore(tmp_path / "sub" / "settings.toml")
    s = Settings(
        clip_folder=tmp_path / "c", episode_length=480, max_quality=720, default_storage="drive"
    )
    store.save(s)
    assert store.load() == s


def test_set_value(tmp_path: Path) -> None:
    store = SettingsStore(tmp_path / "settings.toml")
    assert store.set_value("episode-length", "12:30").episode_length == 750
    assert store.set_value("max-quality", "720p").max_quality == 720
    assert store.set_value("destination", str(tmp_path / "x")).clip_folder == tmp_path / "x"
    assert store.set_value("default-storage", "drive").default_storage == "drive"
    assert store.set_value("default_storage", "").default_storage is None
    assert store.load().episode_length == 750


@pytest.mark.parametrize(
    ("key", "value"),
    [
        ("episode-length", "ten"),
        ("episode-length", "5"),
        ("max-quality", "999"),
        ("max-quality", "hd"),
        ("colour", "red"),
    ],
)
def test_bad_values(key: str, value: str) -> None:
    with pytest.raises(SettingsError) as exc:
        parse_value(Settings(), key, value)
    assert exc.value.hint


def test_corrupt_file(tmp_path: Path) -> None:
    path = tmp_path / "settings.toml"
    path.write_text("clip_folder = [unclosed", encoding="utf-8")
    with pytest.raises(SettingsError, match="not valid TOML"):
        SettingsStore(path).load()
    path.write_text('episode_length = "long"', encoding="utf-8")
    with pytest.raises(SettingsError, match="bad value"):
        SettingsStore(path).load()


def test_as_display() -> None:
    shown = as_display(Settings(episode_length=600, max_quality=1080))
    assert shown["episode-length"] == "10:00"
    assert shown["max-quality"] == "1080p"
    assert shown["default-storage"] == "(none)"


def test_max_clip_length(tmp_path: Path) -> None:
    store = SettingsStore(tmp_path / "settings.toml")
    assert store.load().max_clip_length == 45
    assert store.set_value("max-clip-length", "1:30").max_clip_length == 90
    assert store.load().max_clip_length == 90
    assert store.set_value("clip-cap", "off").max_clip_length is None
    assert store.load().max_clip_length is None
    assert as_display(store.load())["max-clip-length"] == "off"
    assert as_display(Settings())["max-clip-length"] == "45s"
    with pytest.raises(SettingsError):
        store.set_value("max-clip-length", "long")
