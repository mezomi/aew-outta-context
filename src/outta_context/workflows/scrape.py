"""outta scrape SITE PAGE_URL: fetch the page and list the video links on it."""

from __future__ import annotations

from outta_context.core.errors import NoVideosFound
from outta_context.core.reporter import Reporter
from outta_context.library.repository import LibraryRepository
from outta_context.scraping.base import FoundVideo
from outta_context.scraping.fetch import PageFetcher
from outta_context.scraping.prefixes import PrefixStore
from outta_context.scraping.registry import ScraperRegistry


class ScrapeWorkflow:
    def __init__(
        self,
        scrapers: ScraperRegistry,
        fetcher: PageFetcher,
        prefixes: PrefixStore,
        repo: LibraryRepository | None,
        reporter: Reporter,
    ) -> None:
        self.scrapers = scrapers
        self.fetcher = fetcher
        self.prefixes = prefixes
        self.repo = repo
        self.reporter = reporter

    def run(self, site: str, page_url: str) -> list[FoundVideo]:
        scraper = self.scrapers.get(site)
        extra = self.prefixes.for_site(scraper.site)
        self.reporter.step(f"Fetching {page_url}")
        page = self.fetcher.fetch(page_url)
        count = scraper.pattern_count(extra)
        self.reporter.step(f"Looking for {count} {scraper.label} link patterns")
        found = scraper.extract(page, page_url, extra)
        if not found:
            raise NoVideosFound(f"No {scraper.label} videos found on {page_url}.")
        if self.repo is not None:
            self.repo.remember_links([(f.url, f.page_url, f.kind) for f in found])
        return found
