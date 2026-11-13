"""Fetch web pages. The only module in scraping/ that touches the network."""

from __future__ import annotations

from typing import Protocol

import httpx

from outta_context.core.errors import FetchFailed

USER_AGENT = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
    "(KHTML, like Gecko) Chrome/130.0 Safari/537.36"
)


class PageFetcher(Protocol):
    def fetch(self, url: str) -> str: ...


class HttpxFetcher:
    def __init__(self, timeout: float = 20.0, client: httpx.Client | None = None) -> None:
        self._client = client or httpx.Client(
            follow_redirects=True,
            timeout=timeout,
            headers={"User-Agent": USER_AGENT, "Accept-Language": "en-US,en;q=0.9"},
        )

    def fetch(self, url: str) -> str:
        if not url.lower().startswith(("http://", "https://")):
            raise FetchFailed(f"{url} is not a web address.", "Page URLs start with https://")
        try:
            response = self._client.get(url)
        except httpx.HTTPError as exc:
            raise FetchFailed(f"Could not fetch {url}: {exc}") from exc
        if response.status_code >= 400:
            raise FetchFailed(
                f"{url} answered with HTTP {response.status_code}.",
                "The page may need a login or block scripts. Copy the video link from your browser.",
            )
        return response.text
