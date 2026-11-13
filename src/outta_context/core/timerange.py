"""Parse and check clip time ranges such as `90-112`, `1:30-1:52` or `1:02:10-1:02:30`."""

from __future__ import annotations

import re
from dataclasses import dataclass

from outta_context.core.errors import InvalidTimeRange, RangeOutsideVideo

_NUMBER = r"\d+(?:\.\d+)?"
_TIME_RE = re.compile(rf"^(?:(\d+):)?(?:(\d+):)?({_NUMBER})$")


def parse_time(text: str) -> float:
    """Turn `90`, `1:30`, `1:02:10` (or with decimal seconds) into seconds."""
    raw = text.strip()
    if not raw:
        raise InvalidTimeRange("Empty time value.")
    parts = raw.split(":")
    if len(parts) > 3 or not all(parts):
        raise InvalidTimeRange(f'"{text}" is not a time. Use seconds, M:SS or H:MM:SS.')
    if not all(re.fullmatch(r"\d+", p) for p in parts[:-1]) or not re.fullmatch(_NUMBER, parts[-1]):
        raise InvalidTimeRange(f'"{text}" is not a time. Use seconds, M:SS or H:MM:SS.')
    seconds = float(parts[-1])
    if len(parts) >= 2 and seconds >= 60:
        raise InvalidTimeRange(f'"{text}": seconds must be under 60 in M:SS form.')
    minutes = int(parts[-2]) if len(parts) >= 2 else 0
    if len(parts) == 3 and minutes > 59:
        raise InvalidTimeRange(f'"{text}": minutes must be under 60 in H:MM:SS form.')
    hours = int(parts[0]) if len(parts) == 3 else 0
    return hours * 3600 + minutes * 60 + seconds


def format_time(seconds: float) -> str:
    """Format seconds as M:SS or H:MM:SS (fractions are dropped)."""
    total = round(seconds) if seconds >= 0 else 0
    hours, rest = divmod(total, 3600)
    minutes, secs = divmod(rest, 60)
    if hours:
        return f"{hours}:{minutes:02d}:{secs:02d}"
    return f"{minutes}:{secs:02d}"


def format_length(seconds: float) -> str:
    """Short length for status lines: `20s` under a minute, else M:SS."""
    if seconds < 60:
        return f"{round(seconds)}s"
    return format_time(seconds)


@dataclass(frozen=True)
class TimeRange:
    start: float
    end: float

    @property
    def length(self) -> float:
        return self.end - self.start

    def check_within(self, duration: float | None) -> None:
        if duration is None:
            return
        if self.start >= duration:
            raise RangeOutsideVideo(
                f"Start {format_time(self.start)} is past the end of the video "
                f"({format_time(duration)})."
            )
        if self.end > duration + 0.5:
            raise RangeOutsideVideo(
                f"End {format_time(self.end)} is past the end of the video "
                f"({format_time(duration)})."
            )

    def __str__(self) -> str:
        return f"{format_time(self.start)}-{format_time(self.end)}"


def parse_range(text: str) -> TimeRange:
    raw = text.strip().replace(" ", "")
    if raw.count("-") != 1:
        raise InvalidTimeRange(f'"{text}" is not a range. Use START-END, e.g. 1:30-1:52.')
    start_text, end_text = raw.split("-")
    start, end = parse_time(start_text), parse_time(end_text)
    if end <= start:
        raise InvalidTimeRange(
            f'"{text}": the end ({format_time(end)}) must be after the start ({format_time(start)}).'
        )
    return TimeRange(start, end)
