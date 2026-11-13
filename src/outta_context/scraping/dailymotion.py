from __future__ import annotations

import re

from outta_context.scraping.base import LinkPattern, Scraper
from outta_context.sources.base import VideoSource
from outta_context.sources.dailymotion import DailymotionSource

_ID = r"([a-zA-Z0-9]{5,})"


class DailymotionScraper(Scraper):
    site = "dailymotion"
    label = "Dailymotion"
    patterns = (
        LinkPattern(
            re.compile(
                rf"geo\.dailymotion\.com/player(?:/[\w-]+)?\.html\?[^\s\"'<>]*?\bvideo={_ID}", re.I
            ),
            "geo player embed",
        ),
        LinkPattern(re.compile(rf"dailymotion\.com/embed/video/{_ID}", re.I), "embed iframe"),
        LinkPattern(
            re.compile(rf"dailymotion\.com/player\.html\?[^\s\"'<>]*?\bvideo={_ID}", re.I),
            "player embed",
        ),
        LinkPattern(re.compile(rf"dailymotion\.com/video/{_ID}", re.I), "video link"),
        LinkPattern(re.compile(rf"\bdai\.ly/{_ID}", re.I), "short link"),
        LinkPattern(
            re.compile(rf"""data-video(?:-id)?\s*=\s*["']{_ID}["']""", re.I),
            "geo player script",
            requires="dailymotion",
        ),
    )

    def __init__(self) -> None:
        self._source = DailymotionSource()

    @property
    def source(self) -> VideoSource:
        return self._source
