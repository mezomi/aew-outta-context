"""Build a single-file `outta` executable (outta.exe on Windows) into dist/.

    pip install -e ".[dev]" pyinstaller
    python packaging/build_exe.py
"""

from __future__ import annotations

import sys
from pathlib import Path

import PyInstaller.__main__

ROOT = Path(__file__).resolve().parent.parent


def main() -> None:
    args = [
        str(ROOT / "packaging" / "outta_entry.py"),
        "--name", "outta",
        "--onefile",
        "--console",
        "--noconfirm",
        "--clean",
        "--paths", str(ROOT / "src"),
        "--distpath", str(ROOT / "dist"),
        "--workpath", str(ROOT / "build"),
        "--specpath", str(ROOT / "build"),
        # yt-dlp loads extractors and its YouTube JS solver dynamically
        "--collect-submodules", "yt_dlp",
        "--collect-all", "yt_dlp_ejs",
        # keyring finds its OS backends (Windows Credential Manager) via entry points
        "--collect-submodules", "keyring.backends",
        "--copy-metadata", "keyring",
        "--copy-metadata", "outta-context",
        # rich loads width tables lazily
        "--collect-submodules", "rich",
    ]  # fmt: skip
    if sys.platform == "win32":
        args += ["--hidden-import", "win32ctypes.core"]
    PyInstaller.__main__.run(args)


if __name__ == "__main__":
    main()
