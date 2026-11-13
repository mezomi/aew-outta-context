"""Pick the right source for a URL. Add a new site by appending it to DEFAULT_SOURCES."""

from __future__ import annotations

from collections.abc import Iterable

from outta_context.core.errors import UnsupportedUrl
from outta_context.sources.base import VideoSource
from outta_context.sources.dailymotion import DailymotionSource
from outta_context.sources.youtube import YouTubeSource

DEFAULT_SOURCES: tuple[type[VideoSource], ...] = (DailymotionSource, YouTubeSource)


class SourceRegistry:
    def __init__(self, sources: Iterable[VideoSource] | None = None) -> None:
        self.sources = list(sources) if sources is not None else [s() for s in DEFAULT_SOURCES]

    def detect(self, url: str) -> VideoSource:
        for source in self.sources:
            if source.matches(url):
                return source
        labels = " or ".join(s.label for s in self.sources)
        raise UnsupportedUrl(f"Not a {labels} video link: {url}")

    def get(self, name: str) -> VideoSource:
        for source in self.sources:
            if source.name == name:
                return source
        raise KeyError(name)
