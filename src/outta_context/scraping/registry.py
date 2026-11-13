"""All scrapers. `outta scrape <site>` commands are generated from this list."""

from __future__ import annotations

from collections.abc import Iterable

from outta_context.core.errors import UnknownSite
from outta_context.scraping.base import Scraper
from outta_context.scraping.dailymotion import DailymotionScraper
from outta_context.scraping.youtube import YouTubeScraper

DEFAULT_SCRAPERS: tuple[type[Scraper], ...] = (DailymotionScraper, YouTubeScraper)


class ScraperRegistry:
    def __init__(self, scrapers: Iterable[Scraper] | None = None) -> None:
        self.scrapers = list(scrapers) if scrapers is not None else [s() for s in DEFAULT_SCRAPERS]

    def sites(self) -> list[str]:
        return [s.site for s in self.scrapers]

    def get(self, site: str) -> Scraper:
        for scraper in self.scrapers:
            if scraper.site == site.lower():
                return scraper
        raise UnknownSite(
            f'Unknown site "{site}".', "Sites you can use: " + ", ".join(self.sites())
        )
