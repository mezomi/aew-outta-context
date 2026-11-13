from __future__ import annotations

import json
from collections.abc import Callable

import typer
from rich.markup import escape

from outta_context.cli.context import get_services
from outta_context.scraping.registry import ScraperRegistry


def _make_scrape_command(site: str, label: str) -> Callable[..., None]:
    def command(
        ctx: typer.Context,
        page_url: str = typer.Argument(..., help="Web page to search."),
        as_json: bool = typer.Option(False, "--json", help="Print results as JSON."),
    ) -> None:
        services = get_services(ctx.obj)
        if as_json:
            services.reporter.verbosity = min(services.reporter.verbosity, 0)  # type: ignore[assignment]
        found = services.scrape_workflow().run(site, page_url)
        if as_json:
            typer.echo(json.dumps([f.as_dict() for f in found], indent=2))
            return
        services.reporter.success(f"Found {len(found)} video{'s' if len(found) != 1 else ''}:")
        width = max(len(f.url) for f in found)
        for i, f in enumerate(found, start=1):
            services.console.print(f"  {i}. {escape(f.url.ljust(width))}  [dim]({f.kind})[/]")

    command.__doc__ = f"Find {label} video links on a web page (nothing is downloaded)."
    command.__name__ = f"scrape_{site}"
    return command


prefixes_app = typer.Typer(
    help="Extra link prefixes the scrapers treat as videos.", no_args_is_help=True
)


def _check_site(site: str) -> str:
    return ScraperRegistry().get(site).site


@prefixes_app.command("add")
def prefixes_add(
    ctx: typer.Context,
    site: str = typer.Argument(..., help="Site, e.g. dailymotion."),
    prefix: str = typer.Argument(
        ..., help='Start of matching links, e.g. "https://geo.dailymotion.com/player/".'
    ),
) -> None:
    """Register a link prefix for a site."""
    services = get_services(ctx.obj)
    site = _check_site(site)
    services.prefixes.add(site, prefix)
    services.reporter.success(f"Added {prefix} for {site}")


@prefixes_app.command("list")
def prefixes_list(
    ctx: typer.Context, site: str | None = typer.Argument(None, help="Only this site.")
) -> None:
    """Show built-in patterns and registered prefixes."""
    services = get_services(ctx.obj)
    registry = ScraperRegistry()
    sites = [_check_site(site)] if site else registry.sites()
    for name in sites:
        scraper = registry.get(name)
        services.console.print(f"[bold]{scraper.label}[/]")
        for pattern in scraper.patterns:
            services.console.print(f"  [dim]built-in[/]  {pattern.kind}")
        extra = services.prefixes.for_site(name)
        for prefix in extra:
            services.console.print(f"  [green]prefix[/]    {escape(prefix)}")
        if not extra:
            services.console.print("  [dim](no registered prefixes)[/]")


@prefixes_app.command("remove")
def prefixes_remove(
    ctx: typer.Context,
    site: str = typer.Argument(...),
    prefix: str = typer.Argument(...),
) -> None:
    """Remove a registered link prefix."""
    services = get_services(ctx.obj)
    site = _check_site(site)
    services.prefixes.remove(site, prefix)
    services.reporter.success(f"Removed {prefix} from {site}")


def build_app() -> typer.Typer:
    scrape_app = typer.Typer(help="Find video links on a web page.", no_args_is_help=True)
    for scraper in ScraperRegistry().scrapers:
        scrape_app.command(scraper.site, no_args_is_help=True)(
            _make_scrape_command(scraper.site, scraper.label)
        )
    scrape_app.add_typer(prefixes_app, name="prefixes")
    return scrape_app
