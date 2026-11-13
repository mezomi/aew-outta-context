from __future__ import annotations

import re

from outta_context.scraping.base import LinkPattern, Scraper
from outta_context.sources.base import VideoSource
from outta_context.sources.youtube import YouTubeSource

_ID = r"([\w-]{11})(?![\w-])"


class YouTubeScraper(Scraper):
    site = "youtube"
    label = "YouTube"
    patterns = (
        LinkPattern(
            re.compile(rf"youtube(?:-nocookie)?\.com/embed/(?!videoseries){_ID}", re.I),
            "embed iframe",
        ),
        LinkPattern(
            re.compile(rf"youtube\.com/watch\?(?:[^\s\"'<>]*?&)?v={_ID}", re.I), "video link"
        ),
        LinkPattern(re.compile(rf"youtube\.com/shorts/{_ID}", re.I), "short"),
        LinkPattern(re.compile(rf"youtube\.com/live/{_ID}", re.I), "live link"),
        LinkPattern(re.compile(rf"\byoutu\.be/{_ID}", re.I), "short link"),
    )

    def __init__(self) -> None:
        self._source = YouTubeSource()

    @property
    def source(self) -> VideoSource:
        return self._source
