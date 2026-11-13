import io
import json
import subprocess
from pathlib import Path
from typing import Any

import pytest

from outta_context.core.errors import (
    CredentialStoreUnavailable,
    RcloneMissing,
    SettingsError,
    StorageAuthFailed,
    StorageExists,
    StorageNotConfigured,
    UploadFailed,
)
from outta_context.core.reporter import RecordingReporter
from outta_context.library.repository import LibraryRepository
from outta_context.settings.models import Settings
from outta_context.storage.base import StorageConfig, join_remote
from outta_context.storage.credentials import RCLONE_CONFIG_PASS, CredentialStore
from outta_context.storage.local import LocalBackend
from outta_context.storage.rclone import Rclone, RcloneBackend, remote_name, secret_key
from outta_context.storage.registry import StorageRegistry
from outta_context.workflows.upload import UploadService
from tests.conftest import MemoryKeyring

SECRET = "s3cr3t-value"


class Run:
    def __init__(self, returncode: int = 0, stderr: str = "") -> None:
        self.calls: list[tuple[list[str], dict[str, Any]]] = []
        self.returncode, self.stderr = returncode, stderr

    def __call__(self, cmd: list[str], **kw: Any) -> subprocess.CompletedProcess[str]:
        self.calls.append((cmd, kw))
        return subprocess.CompletedProcess(cmd, self.returncode, "rclone v1.68.0\n", self.stderr)


class Proc:
    def __init__(self, lines: list[str], code: int = 0) -> None:
        self.stderr = io.StringIO("".join(line + "\n" for line in lines))
        self.code = code

    def wait(self) -> int:
        return self.code


class Popen:
    def __init__(self, proc: Proc) -> None:
        self.proc = proc
        self.calls: list[tuple[list[str], dict[str, Any]]] = []

    def __call__(self, cmd: list[str], **kw: Any) -> Proc:
        self.calls.append((cmd, kw))
        return self.proc


def creds() -> CredentialStore:
    return CredentialStore(MemoryKeyring())


def rclone(
    tmp_path: Path,
    run: Run | None = None,
    popen: Any = None,
    which: Any = None,
    store: CredentialStore | None = None,
) -> Rclone:
    return Rclone(
        tmp_path / "rclone.conf",
        store or creds(),
        run=run or Run(),
        popen=popen or Popen(Proc([])),
        which=which or (lambda n: "/bin/rclone"),
    )


def assert_no_secret_in_args(calls: list[tuple[list[str], dict[str, Any]]]) -> None:
    for cmd, _ in calls:
        assert all(SECRET not in part for part in cmd)


def test_credential_store() -> None:
    store = creds()
    assert store.get("k") is None
    store.set("k", "v")
    assert store.get("k") == "v"
    store.delete("k")
    store.delete("k")
    assert store.get("k") is None
    assert store.available()[0]

    class Broken:
        def get_password(self, *a: str) -> None:
            raise RuntimeError("no backend")

        def get_keyring(self) -> object:
            class FailKeyring: ...

            return FailKeyring()

    with pytest.raises(CredentialStoreUnavailable):
        CredentialStore(Broken()).get("x")
    assert CredentialStore(Broken()).available()[0] is False


def test_remote_names() -> None:
    assert remote_name("My-Drive") == "outta_my_drive"
    assert (
        join_remote("AEW Outta Context", "", "/Episode 3/", "a.mp4")
        == "AEW Outta Context/Episode 3/a.mp4"
    )


def test_rclone_env_carries_config_password_not_args(tmp_path: Path) -> None:
    store = creds()
    store.set(RCLONE_CONFIG_PASS, SECRET)
    run = Run()
    rc = rclone(tmp_path, run=run, store=store)
    rc.run(["lsd", "outta_drive:"])
    cmd, kw = run.calls[0]
    assert cmd[:3] == ["rclone", "--config", str(tmp_path / "rclone.conf")]
    assert kw["env"]["RCLONE_CONFIG_PASS"] == SECRET
    assert_no_secret_in_args(run.calls)
    assert rc.version() == "rclone v1.68.0"


def test_rclone_missing(tmp_path: Path) -> None:
    rc = rclone(tmp_path, which=lambda n: None)
    with pytest.raises(RcloneMissing) as exc:
        rc.run(["version"])
    assert "winget install Rclone.Rclone" in exc.value.hint
    assert rc.version() is None


def test_ensure_encrypted_passes_password_on_stdin(tmp_path: Path) -> None:
    store = creds()
    run = Run()
    rc = rclone(tmp_path, run=run, store=store)
    assert rc.ensure_encrypted()
    password = store.get(RCLONE_CONFIG_PASS)
    assert password
    cmd, kw = run.calls[0]
    assert cmd[-3:] == ["config", "encryption", "set"]
    assert kw["input"] == f"{password}\n{password}\n"
    assert all(password not in part for part in cmd)
    assert rc.ensure_encrypted()  # already set: no second call
    assert len(run.calls) == 1
    failing = rclone(tmp_path, run=Run(returncode=1), store=creds())
    assert not failing.ensure_encrypted()


def test_gdrive_create_test_remove(tmp_path: Path) -> None:
    run = Run()
    store = creds()
    rc = rclone(tmp_path, run=run, store=store)
    backend = RcloneBackend(StorageConfig("drive", "gdrive", "AEW Outta Context"), rc)
    backend.create()
    cmd, kw = run.calls[0]
    assert cmd[3:] == ["config", "create", "outta_drive", "drive", "scope=drive"]
    assert "capture_output" not in kw  # interactive: browser sign-in
    assert store.get(RCLONE_CONFIG_PASS)  # config encrypted afterwards
    run.calls.clear()
    backend.test()
    ops = [c[3] for c, _ in run.calls]
    assert ops == ["mkdir", "rcat", "deletefile"]
    assert run.calls[0][0][4] == "outta_drive:AEW Outta Context"
    backend.remove()
    assert run.calls[-1][0][3:] == ["config", "delete", "outta_drive"]
    assert backend.describe() == "gdrive:AEW Outta Context"


def test_create_failure_and_test_failure(tmp_path: Path) -> None:
    backend = RcloneBackend(
        StorageConfig("d", "dropbox", "x"),
        rclone(tmp_path, run=Run(returncode=1, stderr="token expired")),
    )
    with pytest.raises(StorageAuthFailed, match="did not finish"):
        backend.create()
    with pytest.raises(StorageAuthFailed, match="did not work"):
        backend.test()


def test_b2_key_lives_in_keychain_and_env_only(tmp_path: Path) -> None:
    run = Run()
    store = creds()
    rc = rclone(tmp_path, run=run, store=store)
    backend = RcloneBackend(StorageConfig("b2", "b2", "bucket/aew", {"account": "acc123"}), rc)
    with pytest.raises(StorageAuthFailed):
        backend.create()
    with pytest.raises(StorageAuthFailed, match="No key"):
        backend.create(prompt_secret=lambda label: "")
    backend.create(prompt_secret=lambda label: SECRET)
    assert store.get(secret_key("b2", "key")) == SECRET
    assert run.calls == []  # nothing written to rclone.conf for B2
    backend.test()
    env = run.calls[0][1]["env"]
    assert env["RCLONE_CONFIG_OUTTA_B2_TYPE"] == "b2"
    assert env["RCLONE_CONFIG_OUTTA_B2_ACCOUNT"] == "acc123"
    assert env["RCLONE_CONFIG_OUTTA_B2_KEY"] == SECRET
    assert_no_secret_in_args(run.calls)
    backend.remove()
    assert store.get(secret_key("b2", "key")) is None


def test_rclone_upload_progress_and_errors(tmp_path: Path) -> None:
    f = tmp_path / "Episode 3.mp4"
    f.write_bytes(b"x" * 1000)
    lines = [
        json.dumps({"level": "info", "stats": {"bytes": 500, "totalBytes": 1000}}),
        "not json",
        json.dumps({"level": "info", "stats": {"bytes": 1000, "totalBytes": 1000}}),
    ]
    popen = Popen(Proc(lines))
    backend = RcloneBackend(StorageConfig("drive", "gdrive", "AEW"), rclone(tmp_path, popen=popen))
    seen: list[tuple[float, float | None]] = []
    where = backend.upload(f, "Episode 3", progress=lambda a, b: seen.append((a, b)))
    assert where == "outta_drive:AEW/Episode 3/Episode 3.mp4"
    cmd = popen.calls[0][0]
    assert cmd[3:6] == ["copyto", str(f), "outta_drive:AEW/Episode 3/Episode 3.mp4"]
    assert seen == [(500, 1000), (1000, 1000), (1000, 1000)]
    bad = Popen(Proc([json.dumps({"level": "error", "msg": "couldn't connect"})], code=1))
    with pytest.raises(UploadFailed, match="couldn't connect"):
        RcloneBackend(StorageConfig("drive", "gdrive", "AEW"), rclone(tmp_path, popen=bad)).upload(
            f
        )
    auth = Popen(Proc([json.dumps({"level": "error", "msg": "oauth2: invalid_grant"})], code=1))
    with pytest.raises(StorageAuthFailed):
        RcloneBackend(StorageConfig("drive", "gdrive", "AEW"), rclone(tmp_path, popen=auth)).upload(
            f
        )


def test_local_backend(tmp_path: Path) -> None:
    dest = tmp_path / "nas"
    backend = LocalBackend(StorageConfig("nas", "local", str(dest)))
    f = tmp_path / "a.mp4"
    f.write_bytes(b"x" * 10)
    with pytest.raises(StorageAuthFailed):
        backend.test()
    with pytest.raises(UploadFailed):
        backend.upload(f)
    dest.mkdir()
    backend.test()
    seen: list[float] = []
    where = backend.upload(f, "Episode 1/clips", progress=lambda a, b: seen.append(a))
    assert Path(where).read_bytes() == b"x" * 10
    assert seen == [10]
    assert not list(dest.rglob("*.part"))


def test_registry(tmp_path: Path) -> None:
    reg = StorageRegistry(tmp_path / "storage.toml", lambda: rclone(tmp_path))
    assert reg.list() == []
    reg.add(StorageConfig("drive", "gdrive", "AEW Outta Context"))
    reg.add(StorageConfig("b2", "b2", "bucket", {"account": "acc"}))
    with pytest.raises(StorageExists):
        reg.add(StorageConfig("drive", "gdrive", "x"))
    again = StorageRegistry(tmp_path / "storage.toml", lambda: rclone(tmp_path))
    assert again.get("b2").options == {"account": "acc"}
    assert isinstance(again.backend(again.get("drive")), RcloneBackend)
    assert isinstance(again.backend(StorageConfig("l", "local", "/x")), LocalBackend)
    with pytest.raises(SettingsError):
        again.backend(StorageConfig("x", "ftp", ""))
    again.remove("b2")
    with pytest.raises(StorageNotConfigured):
        again.get("b2")
    assert "SECRET" not in (tmp_path / "storage.toml").read_text()
    assert again.validate_name("drive-2") == "drive-2"
    with pytest.raises(SettingsError):
        again.validate_name("bad name!")
    (tmp_path / "storage.toml").write_text("[[", encoding="utf-8")
    with pytest.raises(SettingsError):
        again.list()


def test_upload_service_skips_repeats(tmp_path: Path, repo: LibraryRepository) -> None:
    dest = tmp_path / "backup"
    dest.mkdir()
    reg = StorageRegistry(tmp_path / "storage.toml", lambda: rclone(tmp_path))
    reg.add(StorageConfig("nas", "local", str(dest)))
    reporter = RecordingReporter()
    svc = UploadService(reg, repo, Settings(), reporter)
    f = tmp_path / "a.mp4"
    f.write_bytes(b"abc")
    with pytest.raises(StorageNotConfigured):
        svc.upload_file(f)
    svc.settings.default_storage = "nas"
    svc.upload_file(f)
    svc.upload_file(f)
    assert reporter.texts("success")[-1].startswith("Already uploaded a.mp4 to nas")
    assert len(reporter.tasks) == 1
    with pytest.raises(UploadFailed, match="does not exist"):
        svc.upload_file(tmp_path / "missing.mp4")


def test_upload_episode_folder_layout(tmp_path: Path, repo: LibraryRepository) -> None:
    dest = tmp_path / "backup"
    dest.mkdir()
    reg = StorageRegistry(tmp_path / "storage.toml", lambda: rclone(tmp_path))
    reg.add(StorageConfig("nas", "local", str(dest)))
    svc = UploadService(reg, repo, Settings(default_storage="nas"), RecordingReporter())
    clip_file = tmp_path / "Table spot.mp4"
    clip_file.write_bytes(b"c")
    clip = repo.add_clip("Table spot", "youtube", "u", 0, 1, 1, clip_file)
    ep = repo.create_episode("Episode 3", 600)
    with pytest.raises(UploadFailed, match="no built video"):
        svc.upload_episode(ep, [clip])
    out = tmp_path / "Episode 3.mp4"
    out.write_bytes(b"e")
    ep = repo.mark_built(ep.id, out)
    svc.upload_episode(ep, [clip])
    assert (dest / "Episode 3" / "Episode 3.mp4").exists()
    assert (dest / "Episode 3" / "clips" / "Table spot.mp4").exists()
