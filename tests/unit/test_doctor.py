from datetime import date
from pathlib import Path

from outta_context.settings.models import Settings
from outta_context.storage.base import StorageConfig
from outta_context.storage.credentials import CredentialStore
from outta_context.storage.registry import StorageRegistry
from outta_context.workflows.doctor import FAIL, OK, WARN, Doctor, ytdlp_age_days
from tests.conftest import FakeFfmpeg, MemoryKeyring


def make(
    tmp_path: Path, which: dict[str, bool], ytdlp: str | None = "2026.11.01", ffmpeg_ok: bool = True
) -> tuple[Doctor, StorageRegistry]:
    reg = StorageRegistry(tmp_path / "storage.toml", lambda: None)  # type: ignore[arg-type,return-value]
    doctor = Doctor(
        FakeFfmpeg(is_available=ffmpeg_ok),
        Settings(clip_folder=tmp_path / "clips"),  # type: ignore[arg-type]
        reg,
        CredentialStore(MemoryKeyring()),
        which=lambda n: f"/bin/{n}" if which.get(n) else None,
        ytdlp_version=lambda: ytdlp,
        impersonation=lambda: "0.17.0" if ffmpeg_ok else None,
    )
    return doctor, reg


def by_name(checks):  # type: ignore[no-untyped-def]
    return {c.name: c for c in checks}


def test_age() -> None:
    assert ytdlp_age_days("2026.10.01", date(2026, 11, 13)) == 43
    assert ytdlp_age_days("2026.10.01.232", date(2026, 10, 2)) == 1
    assert ytdlp_age_days("nightly") is None


def test_all_good(tmp_path: Path) -> None:
    doctor, _ = make(
        tmp_path, {"deno": True, "rclone": True}, ytdlp=date.today().strftime("%Y.%m.%d")
    )
    checks = by_name(doctor.run())
    assert checks["ffmpeg"].state == OK
    assert checks["ffmpeg"].message == "7.1"
    assert checks["yt-dlp"].state == OK
    assert checks["js runtime"].state == OK
    assert checks["clip folder"].state in (OK, WARN)
    assert checks["keychain"].state == OK
    assert checks["rclone"].state == OK
    assert checks["impersonation"].state == OK


def test_problems_come_with_fixes(tmp_path: Path) -> None:
    doctor, reg = make(tmp_path, {}, ytdlp="2025.01.01", ffmpeg_ok=False)
    reg.add(StorageConfig("nas", "local", str(tmp_path / "missing")))
    reg.add(StorageConfig("drive", "gdrive", "AEW"))
    checks = by_name(doctor.run(test_storage=False))
    assert checks["ffmpeg"].state == FAIL
    assert "winget install ffmpeg" in checks["ffmpeg"].fix
    assert checks["yt-dlp"].state == WARN
    assert "days old" in checks["yt-dlp"].message
    assert checks["js runtime"].state == WARN
    assert checks["impersonation"].state == WARN
    assert "Dailymotion" in checks["impersonation"].message
    assert checks["rclone"].state == FAIL  # a cloud storage needs it
    assert "Rclone.Rclone" in checks["rclone"].fix
    assert "storage nas" not in checks
    checks = by_name(make(tmp_path / "fresh", {}, ytdlp=None)[0].run())
    assert checks["yt-dlp"].state == FAIL
    assert checks["rclone"].state == WARN  # no storages yet: optional


def test_storage_logins_tested(tmp_path: Path) -> None:
    doctor, reg = make(tmp_path, {"rclone": True})
    (tmp_path / "ok").mkdir()
    reg.add(StorageConfig("good", "local", str(tmp_path / "ok")))
    reg.add(StorageConfig("bad", "local", str(tmp_path / "gone")))
    checks = by_name(doctor.run())
    assert checks["storage good"].state == OK
    assert checks["storage bad"].state == FAIL
    assert checks["storage bad"].fix


def test_unwritable_clip_folder(tmp_path: Path) -> None:
    blocker = tmp_path / "file"
    blocker.write_text("x")
    doctor, _ = make(tmp_path, {})
    doctor.settings.clip_folder = blocker / "clips"
    check = doctor.check_clip_folder()
    assert check.state == FAIL
    assert "set-destination" in check.fix
