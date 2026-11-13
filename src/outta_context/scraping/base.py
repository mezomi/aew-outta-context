"""Finding video links in HTML: built-in patterns plus user-registered prefixes."""

from __future__ import annotations

import abc
import html
import re
from dataclasses import dataclass
from urllib.parse import parse_qs, urlsplit

from outta_context.sources.base import VideoSource

_URL_RE = re.compile(r"""(?:https?:)?//[^\s"'<>()\\]+""", re.I)
_ID_RE = re.compile(r"^[\w-]{5,}$")


@dataclass(frozen=True)
class LinkPattern:
    regex: re.Pattern[str]
    kind: str  # shown next to results, e.g. "geo player embed"
    requires: str | None = None  # only used if this text appears on the page


@dataclass(frozen=True)
class FoundVideo:
    url: str
    video_id: str
    kind: str
    page_url: str

    def as_dict(self) -> dict[str, str]:
        return {"url": self.url, "id": self.video_id, "kind": self.kind, "page_url": self.page_url}


def id_from_link(link: str) -> str | None:
    """Video ID from a link: its `video=`/`v=` parameter, or else the last part of its path."""
    parts = urlsplit(link if "://" in link else "https:" + link)
    query = parse_qs(parts.query)
    for key in ("video", "v", "id"):
        if query.get(key):
            candidate = query[key][0]
            return candidate if _ID_RE.match(candidate) else None
    segments = [s for s in parts.path.split("/") if s]
    if not segments:
        return None
    last = re.sub(r"\.(html?|js|php)$", "", segments[-1], flags=re.I)
    return last if _ID_RE.match(last) else None


def _strip_scheme(url: str) -> str:
    return re.sub(r"^(?:https?:)?//", "", url.strip(), flags=re.I).lower()


class Scraper(abc.ABC):
    site: str
    label: str
    patterns: tuple[LinkPattern, ...]

    @property
    @abc.abstractmethod
    def source(self) -> VideoSource: ...

    def pattern_count(self, prefixes: list[str]) -> int:
        return len(self.patterns) + len(prefixes)

    def extract(
        self, page_html: str, page_url: str, prefixes: list[str] | None = None
    ) -> list[FoundVideo]:
        text = html.unescape(page_html).replace("\\/", "/")
        hits: list[tuple[int, str, str]] = []
        lowered = text.lower()
        for pattern in self.patterns:
            if pattern.requires and pattern.requires not in lowered:
                continue
            for match in pattern.regex.finditer(text):
                hits.append((match.start(), match.group(1), pattern.kind))
        if prefixes:
            stripped = [_strip_scheme(p) for p in prefixes]
            for match in _URL_RE.finditer(text):
                link = match.group(0)
                bare = _strip_scheme(link)
                if any(bare.startswith(p) for p in stripped):
                    video_id = id_from_link(link)
                    if video_id:
                        hits.append((match.start(), video_id, "registered prefix"))
        hits.sort(key=lambda h: h[0])
        seen: set[str] = set()
        found: list[FoundVideo] = []
        for _, video_id, kind in hits:
            url = self.source.url_for_id(video_id)
            if url in seen:
                continue
            seen.add(url)
            found.append(FoundVideo(url=url, video_id=video_id, kind=kind, page_url=page_url))
        return found
