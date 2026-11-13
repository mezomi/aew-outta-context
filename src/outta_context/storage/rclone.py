"""Google Drive, OneDrive, Dropbox and Backblaze B2 through rclone.

The tool keeps its own rclone config (rclone.conf in the app data folder), encrypted with a
random password stored in the system keychain. The password and any API keys reach rclone only
through environment variables, never files or command-line arguments.
"""

from __future__ import annotations

import json
import os
import re
import secrets
import shutil
import subprocess
import uuid
from collections.abc import Callable, Mapping, Sequence
from pathlib import Path
from typing import IO, Any

from outta_context.core.errors import (
    CredentialStoreUnavailable,
    RcloneMissing,
    StorageAuthFailed,
    UploadFailed,
)
from outta_context.storage.base import ProgressCallback, StorageBackend, StorageConfig, join_remote
from outta_context.storage.credentials import RCLONE_CONFIG_PASS, CredentialStore

RCLONE_TYPES = {"gdrive": "drive", "onedrive": "onedrive", "dropbox": "dropbox", "b2": "b2"}
OAUTH_PROVIDERS = {"gdrive", "onedrive", "dropbox"}


def remote_name(storage_name: str) -> str:
    return "outta_" + re.sub(r"[^a-z0-9]", "_", storage_name.lower())


def secret_key(storage_name: str, option: str) -> str:
    return f"storage:{storage_name}:{option}"


class Rclone:
    """Runs rclone with our config file and the secrets in its environment."""

    def __init__(
        self,
        config_path: Path,
        credentials: CredentialStore,
        exe: str = "rclone",
        run: Callable[..., subprocess.CompletedProcess[str]] = subprocess.run,
        popen: Callable[..., Any] = subprocess.Popen,
        which: Callable[[str], str | None] = shutil.which,
        detail: Callable[[str], None] | None = None,
    ) -> None:
        self.config_path = config_path
        self.credentials = credentials
        self.exe = exe
        self._run = run
        self._popen = popen
        self._which = which
        self._detail = detail

    def available(self) -> bool:
        return self._which(self.exe) is not None

    def ensure_available(self) -> None:
        if not self.available():
            raise RcloneMissing("rclone was not found on PATH.")

    def version(self) -> str | None:
        if not self.available():
            return None
        result = self.run(["version"], check=False)
        lines = (result.stdout or "").splitlines()
        return lines[0] if lines else None

    def env(self, extra: Mapping[str, str] | None = None) -> dict[str, str]:
        env = dict(os.environ)
        env.pop("RCLONE_CONFIG_PASS", None)
        try:
            password = self.credentials.get(RCLONE_CONFIG_PASS)
        except CredentialStoreUnavailable:
            password = None
        if password:
            env["RCLONE_CONFIG_PASS"] = password
        env["RCLONE_ASK_PASSWORD"] = "false"
        if extra:
            env.update(extra)
        return env

    def command(self, args: Sequence[str]) -> list[str]:
        return [self.exe, "--config", str(self.config_path), *args]

    def run(
        self,
        args: Sequence[str],
        extra_env: Mapping[str, str] | None = None,
        check: bool = True,
        interactive: bool = False,
        input_text: str | None = None,
    ) -> subprocess.CompletedProcess[str]:
        self.ensure_available()
        cmd = self.command(args)
        if self._detail:
            self._detail("$ " + subprocess.list2cmdline(cmd))
        kwargs: dict[str, Any] = {"env": self.env(extra_env), "text": True}
        if not interactive:
            kwargs.update(capture_output=True, encoding="utf-8", errors="replace")
        if input_text is not None:
            kwargs["input"] = input_text
        result = self._run(cmd, **kwargs)
        if check and result.returncode != 0:
            tail = "\n".join(
                ((result.stderr or "") if not interactive else "").strip().splitlines()[-3:]
            )
            raise UploadFailed(
                f"rclone {args[0]} failed (exit {result.returncode}). {tail}".strip()
            )
        return result

    def ensure_encrypted(self) -> bool:
        """Encrypt our rclone config with a random password kept in the keychain.

        Returns False if there is no usable keychain (config then stays unencrypted).
        """
        try:
            if self.credentials.get(RCLONE_CONFIG_PASS):
                return True
            password = secrets.token_urlsafe(32)
        except CredentialStoreUnavailable:
            return False
        self.config_path.parent.mkdir(parents=True, exist_ok=True)
        self.config_path.touch(exist_ok=True)
        # rclone reads the new password twice from stdin when stdin is not a terminal.
        result = self._run(
            self.command(["config", "encryption", "set"]),
            env={**self.env(), "RCLONE_ASK_PASSWORD": "true"},
            input=f"{password}\n{password}\n",
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
        )
        if result.returncode != 0:
            return False
        self.credentials.set(RCLONE_CONFIG_PASS, password)
        return True


class RcloneBackend(StorageBackend):
    def __init__(self, config: StorageConfig, rclone: Rclone) -> None:
        super().__init__(config)
        self.rclone = rclone
        self.remote = remote_name(config.name)

    # Env vars that define or complete the remote at run time (API keys stay in the keychain).
    def remote_env(self) -> dict[str, str]:
        if self.config.provider != "b2":
            return {}
        prefix = f"RCLONE_CONFIG_{self.remote.upper()}_"
        env = {prefix + "TYPE": "b2", prefix + "ACCOUNT": self.config.options.get("account", "")}
        key = self.rclone.credentials.get(secret_key(self.config.name, "key"))
        if key:
            env[prefix + "KEY"] = key
        return env

    def target(self, subfolder: str = "", filename: str = "") -> str:
        return f"{self.remote}:{join_remote(self.config.folder, subfolder, filename)}"

    def describe(self) -> str:
        return f"{self.config.provider}:{self.config.folder or '/'}"

    def create(self, prompt_secret: Callable[[str], str] | None = None) -> None:
        """Set up the login. OAuth providers open a browser; B2 asks for the key (hidden)."""
        self.rclone.ensure_available()
        if self.config.provider in OAUTH_PROVIDERS:
            args = ["config", "create", self.remote, RCLONE_TYPES[self.config.provider]]
            if self.config.provider == "gdrive":
                args.append("scope=drive")
            result = self.rclone.run(args, check=False, interactive=True)
            if result.returncode != 0:
                raise StorageAuthFailed(
                    f"Signing in to {self.config.provider} did not finish.",
                    "Run the command again and approve access in the browser window.",
                )
            self.rclone.ensure_encrypted()
        elif self.config.provider == "b2":
            if prompt_secret is None:
                raise StorageAuthFailed("A Backblaze application key is needed.")
            key = prompt_secret("Backblaze application key")
            if not key:
                raise StorageAuthFailed("No key entered.", "Create one in the Backblaze console.")
            self.rclone.credentials.set(secret_key(self.config.name, "key"), key)

    def remove(self) -> None:
        if self.config.provider in OAUTH_PROVIDERS and self.rclone.available():
            self.rclone.run(["config", "delete", self.remote], check=False)
        self.rclone.credentials.delete(secret_key(self.config.name, "key"))

    def test(self) -> None:
        name = f".outta-test-{uuid.uuid4().hex[:8]}.txt"
        env = self.remote_env()
        try:
            self.rclone.run(["mkdir", self.target()], extra_env=env)
            self.rclone.run(["rcat", self.target(filename=name)], extra_env=env, input_text="ok\n")
            self.rclone.run(["deletefile", self.target(filename=name)], extra_env=env)
        except UploadFailed as exc:
            raise StorageAuthFailed(
                f'Storage "{self.config.name}" did not work: {exc.message}'
            ) from exc

    def upload_command(self, local_path: Path, subfolder: str = "") -> list[str]:
        return self.rclone.command(
            [
                "copyto",
                str(local_path),
                self.target(subfolder, local_path.name),
                "--use-json-log",
                "--stats",
                "1s",
                "-v",
            ]
        )

    def upload(
        self, local_path: Path, subfolder: str = "", progress: ProgressCallback | None = None
    ) -> str:
        self.rclone.ensure_available()
        cmd = self.upload_command(local_path, subfolder)
        total = float(local_path.stat().st_size)
        if self.rclone._detail:
            self.rclone._detail("$ " + subprocess.list2cmdline(cmd))
        proc = self.rclone._popen(
            cmd,
            env=self.rclone.env(self.remote_env()),
            stdout=subprocess.DEVNULL,
            stderr=subprocess.PIPE,
            text=True,
            encoding="utf-8",
            errors="replace",
        )
        errors = _read_rclone_log(proc.stderr, total, progress)
        code = proc.wait()
        if code != 0:
            message = errors[-1] if errors else f"exit code {code}"
            if any(
                w in message.lower() for w in ("token", "unauthor", "401", "403", "invalid_grant")
            ):
                raise StorageAuthFailed(f"Upload to {self.config.name} was refused: {message}")
            raise UploadFailed(f"Upload to {self.config.name} failed: {message}")
        if progress:
            progress(total, total)
        return self.target(subfolder, local_path.name)


def _read_rclone_log(
    stream: IO[str] | None, total: float, progress: ProgressCallback | None
) -> list[str]:
    errors: list[str] = []
    if stream is None:
        return errors
    for line in stream:
        try:
            entry = json.loads(line)
        except ValueError:
            if line.strip():
                errors.append(line.strip())
            continue
        stats = entry.get("stats")
        if stats and progress:
            progress(float(stats.get("bytes", 0)), float(stats.get("totalBytes") or total))
        if entry.get("level") in ("error", "critical"):
            errors.append(str(entry.get("msg", "")).strip())
    return errors
