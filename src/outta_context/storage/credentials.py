"""CredentialStore: the only module that reads or writes secrets (via keyring).

On Windows keyring uses Credential Manager. Secrets never go to TOML, SQLite, logs or argv.
"""

from __future__ import annotations

from typing import Any

from outta_context.core.errors import CredentialStoreUnavailable

SERVICE = "outta-context"
RCLONE_CONFIG_PASS = "rclone-config-pass"


class CredentialStore:
    def __init__(self, backend: Any | None = None, service: str = SERVICE) -> None:
        if backend is None:
            import keyring

            backend = keyring
        self._keyring = backend
        self.service = service

    def _call(self, fn: str, *args: str) -> Any:
        try:
            return getattr(self._keyring, fn)(self.service, *args)
        except Exception as exc:
            name = type(exc).__name__
            if name == "PasswordDeleteError":
                return None
            raise CredentialStoreUnavailable(f"Could not use the system keychain: {exc}") from exc

    def get(self, key: str) -> str | None:
        value = self._call("get_password", key)
        return str(value) if value is not None else None

    def set(self, key: str, value: str) -> None:
        self._call("set_password", key, value)

    def delete(self, key: str) -> None:
        if self.get(key) is not None:
            self._call("delete_password", key)

    def available(self) -> tuple[bool, str]:
        try:
            backend = self._keyring.get_keyring()
        except Exception as exc:
            return False, str(exc)
        kind = type(backend)
        name = f"{kind.__module__.removeprefix('keyring.backends.')}.{kind.__name__}"
        if any(w in name.lower() for w in ("fail", "null")):
            return False, name
        return True, name
