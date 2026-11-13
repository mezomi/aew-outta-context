from __future__ import annotations

import typer
from rich.markup import escape

from outta_context.cli.context import get_services
from outta_context.workflows.doctor import FAIL, OK, WARN

_ICONS = {OK: "[green]✓[/]", WARN: "[yellow]![/]", FAIL: "[red]✗[/]"}


def doctor(
    ctx: typer.Context,
    no_storage: bool = typer.Option(False, "--no-storage", help="Skip testing storage logins."),
) -> None:
    """Check ffmpeg, yt-dlp, rclone and storage logins, and print fixes."""
    services = get_services(ctx.obj)
    checks = services.doctor().run(test_storage=not no_storage)
    width = max(len(c.name) for c in checks)
    for c in checks:
        services.console.print(f"{_ICONS[c.state]} {c.name.ljust(width)}  {escape(c.message)}")
        if c.fix and c.state != OK:
            services.console.print(f"  {' ' * width}  [dim]fix:[/] {escape(c.fix)}")
    failed = [c for c in checks if c.state == FAIL]
    if failed:
        services.console.print(f"\n[red]{len(failed)} problem(s) found.[/]")
        raise typer.Exit(1)
    services.console.print("\n[green]All required tools are ready.[/]")
