"""Turn clip titles into file names that are safe on Windows (and everywhere else)."""

from __future__ import annotations

import re

from outta_context.core.errors import InvalidTitle

_ILLEGAL = re.compile(r'[<>:"/\\|?*\x00-\x1f]')
_RESERVED = (
    {"CON", "PRN", "AUX", "NUL"}
    | {f"COM{i}" for i in range(1, 10)}
    | {f"LPT{i}" for i in range(1, 10)}
)
MAX_LENGTH = 120


def clean_title(title: str) -> str:
    """Trim and collapse whitespace; refuse empty titles."""
    cleaned = " ".join(title.split())
    if not cleaned:
        raise InvalidTitle("The clip title is empty.")
    return cleaned


def safe_filename(title: str, max_length: int = MAX_LENGTH) -> str:
    """File name stem for a title: illegal characters become `_`, no trailing dots or spaces."""
    name = _ILLEGAL.sub("_", clean_title(title))
    name = name[:max_length].rstrip(" .")
    if not name.strip("_ "):
        raise InvalidTitle(f'"{title}" has no usable characters for a file name.')
    if name.split(".")[0].upper() in _RESERVED:
        name = f"{name}_"
    return name
