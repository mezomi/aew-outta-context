from __future__ import annotations

from pathlib import Path

import typer
from rich.markup import escape
from rich.table import Table

from outta_context.cli.context import get_services
from outta_context.settings.store import KEYS, as_display

app = typer.Typer(help="Clip folder, episode length and quality.", no_args_is_help=True)


@app.command("set-destination", no_args_is_help=True)
def set_destination(
    ctx: typer.Context, folder: Path = typer.Argument(..., help="Folder for clips.")
) -> None:
    """Where clips (and built episodes, in an Episodes subfolder) are saved."""
    services = get_services(ctx.obj)
    folder = folder.expanduser().resolve()
    folder.mkdir(parents=True, exist_ok=True)
    services.settings_store.set_value("destination", str(folder))
    services.reporter.success(f"Clips will be saved in {folder}")


@app.command("set", no_args_is_help=True)
def set_(
    ctx: typer.Context,
    key: str = typer.Argument(..., help="One of: " + ", ".join(KEYS)),
    value: str = typer.Argument(...),
) -> None:
    """Change a setting, e.g. `outta config set episode-length 10:00`."""
    services = get_services(ctx.obj)
    settings = services.settings_store.set_value(key, value)
    shown = as_display(settings)[
        key.lower().replace("_", "-").replace("clip-folder", "destination")
    ]
    services.reporter.success(f"{key} = {shown}")


@app.command("show")
def show(ctx: typer.Context) -> None:
    """Print all settings and where the data files are."""
    services = get_services(ctx.obj)
    table = Table(box=None, pad_edge=False, show_header=False)
    table.add_column(style="bold")
    table.add_column()
    table.add_column(style="dim")
    for key, value in as_display(services.settings).items():
        table.add_row(key, escape(value), KEYS[key])
    services.console.print(table)
    services.console.print(f"\n[dim]Data folder:[/] {escape(str(services.paths.data_dir))}")
