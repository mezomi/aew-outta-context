"""Builds the services the commands use. Tests replace pieces through `AppServices` attributes."""

from __future__ import annotations

from collections.abc import Callable
from functools import cached_property

from rich.console import Console

from outta_context.core.paths import AppPaths
from outta_context.core.reporter import ConsoleReporter, Verbosity
from outta_context.library.episodes import EpisodeService
from outta_context.library.repository import LibraryRepository
from outta_context.media.downloader import ClipDownloader
from outta_context.media.ffmpeg import Ffmpeg
from outta_context.scraping.fetch import HttpxFetcher, PageFetcher
from outta_context.scraping.prefixes import PrefixStore
from outta_context.scraping.registry import ScraperRegistry
from outta_context.settings.models import Settings
from outta_context.settings.store import SettingsStore
from outta_context.sources.registry import SourceRegistry
from outta_context.storage.credentials import CredentialStore
from outta_context.storage.rclone import Rclone
from outta_context.storage.registry import StorageRegistry
from outta_context.workflows.build_episode import BuildEpisodeWorkflow
from outta_context.workflows.doctor import Doctor
from outta_context.workflows.make_clip import MakeClipWorkflow
from outta_context.workflows.scrape import ScrapeWorkflow
from outta_context.workflows.upload import UploadService


class AppServices:
    def __init__(self, verbosity: Verbosity = Verbosity.NORMAL, console: Console | None = None):
        self.verbosity = verbosity
        self.console = console or Console(highlight=False)
        self.reporter = ConsoleReporter(self.console, verbosity)
        self.paths = AppPaths.default()

    @property
    def verbose(self) -> bool:
        return self.verbosity >= Verbosity.VERBOSE

    @cached_property
    def settings_store(self) -> SettingsStore:
        return SettingsStore(self.paths.settings_file)

    @cached_property
    def settings(self) -> Settings:
        return self.settings_store.load()

    @cached_property
    def repo(self) -> LibraryRepository:
        self.paths.ensure()
        return LibraryRepository(self.paths.library_db)

    @cached_property
    def episodes(self) -> EpisodeService:
        return EpisodeService(self.repo, self.settings.episode_length)

    @cached_property
    def sources(self) -> SourceRegistry:
        return SourceRegistry()

    @cached_property
    def scrapers(self) -> ScraperRegistry:
        return ScraperRegistry()

    @cached_property
    def prefixes(self) -> PrefixStore:
        return PrefixStore(self.paths.prefixes_file)

    @cached_property
    def fetcher(self) -> PageFetcher:
        return HttpxFetcher()

    @cached_property
    def downloader(self) -> ClipDownloader:
        return ClipDownloader()

    @cached_property
    def ffmpeg(self) -> Ffmpeg:
        return Ffmpeg(detail=self.reporter.detail)

    @cached_property
    def credentials(self) -> CredentialStore:
        return CredentialStore()

    def make_rclone(self) -> Rclone:
        return Rclone(self.paths.rclone_config, self.credentials, detail=self.reporter.detail)

    @cached_property
    def storages(self) -> StorageRegistry:
        return StorageRegistry(self.paths.storage_file, self.make_rclone)

    @cached_property
    def uploader(self) -> UploadService:
        return UploadService(self.storages, self.repo, self.settings, self.reporter)

    def make_clip_workflow(self) -> MakeClipWorkflow:
        return MakeClipWorkflow(
            self.sources,
            self.downloader,
            self.ffmpeg,
            self.repo,
            self.episodes,
            self.settings,
            self.reporter,
            self.uploader,
        )

    def build_workflow(self) -> BuildEpisodeWorkflow:
        return BuildEpisodeWorkflow(
            self.episodes,
            self.ffmpeg,
            self.settings,
            self.paths.cache_dir,
            self.reporter,
            self.uploader,
        )

    def scrape_workflow(self) -> ScrapeWorkflow:
        return ScrapeWorkflow(self.scrapers, self.fetcher, self.prefixes, self.repo, self.reporter)

    def doctor(self) -> Doctor:
        return Doctor(self.ffmpeg, self.settings, self.storages, self.credentials)


# Tests swap this to inject fakes.
services_factory: Callable[[Verbosity], AppServices] = AppServices


def get_services(ctx_obj: object) -> AppServices:
    assert isinstance(ctx_obj, AppServices)
    return ctx_obj
