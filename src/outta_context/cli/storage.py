from __future__ import annotations

import typer
from rich.markup import escape
from rich.table import Table

from outta_context.cli.context import get_services
from outta_context.core.errors import SettingsError
from outta_context.storage.base import StorageConfig
from outta_context.storage.rclone import RcloneBackend
from outta_context.storage.registry import PROVIDER_LABELS, PROVIDERS

app = typer.Typer(help="Where clips and episodes are uploaded.", no_args_is_help=True)


@app.command("add", no_args_is_help=True)
def add(
    ctx: typer.Context,
    name: str = typer.Argument(..., help="Short name, e.g. drive."),
    provider: str = typer.Option(..., "--provider", "-p", help="|".join(PROVIDERS)),
    folder: str = typer.Option(
        "AEW Outta Context",
        "--folder",
        "-f",
        help="Remote folder (for local: the destination path).",
    ),
    account: str | None = typer.Option(None, "--account", help="Backblaze B2 key ID."),
    skip_test: bool = typer.Option(False, "--skip-test", help="Don't test after adding."),
) -> None:
    """Register a storage and sign in once (browser sign-in for Drive/OneDrive/Dropbox)."""
    services = get_services(ctx.obj)
    registry = services.storages
    registry.validate_name(name)
    provider = provider.lower()
    if provider not in PROVIDERS:
        raise SettingsError(
            f'Unknown provider "{provider}".', "Use one of: " + ", ".join(PROVIDERS)
        )
    if any(c.name == name for c in registry.list()):
        registry.add(StorageConfig(name, provider, folder))  # raises StorageExists
    options: dict[str, str] = {}
    if provider == "b2":
        account = account or typer.prompt("Backblaze key ID (not secret)")
        options["account"] = account
    if provider == "local" and folder == "AEW Outta Context":
        raise SettingsError("Local storage needs a folder.", 'Pass --folder "D:\\AEW backup".')
    config = StorageConfig(name, provider, folder, options)
    backend = registry.backend(config)
    r = services.reporter
    if isinstance(backend, RcloneBackend):
        r.step(f"Checking rclone{'' if backend.rclone.available() else ' (not found)'}")
        backend.rclone.ensure_available()
        if provider != "b2":
            r.step(f"Opening your browser to sign in to {PROVIDER_LABELS[provider]}...")
        backend.create(prompt_secret=lambda label: typer.prompt(label, hide_input=True))
    registry.add(config)
    r.success(f"Added storage {name} ({PROVIDER_LABELS[provider]}, folder {folder})")
    if not skip_test:
        r.step(f"Testing {name}")
        backend.test()
        r.success(f"{name} works")
    if not services.settings.default_storage:
        services.settings_store.set_value("default-storage", name)
        r.success(f"{name} is now the default storage")


@app.command("list")
def list_(ctx: typer.Context) -> None:
    """Show configured storages."""
    services = get_services(ctx.obj)
    configs = services.storages.list()
    if not configs:
        services.console.print(
            "No storage yet. Add one with `outta storage add drive --provider gdrive`."
        )
        return
    default = services.settings.default_storage
    table = Table(box=None, pad_edge=False, header_style="dim")
    for col in ("Name", "Provider", "Folder"):
        table.add_column(col)
    for c in configs:
        mark = " [cyan](default)[/]" if c.name == default else ""
        table.add_row(
            escape(c.name) + mark, PROVIDER_LABELS.get(c.provider, c.provider), escape(c.folder)
        )
    services.console.print(table)


@app.command("test", no_args_is_help=True)
def test(ctx: typer.Context, name: str = typer.Argument(...)) -> None:
    """Check a storage login works (uploads and deletes a tiny file)."""
    services = get_services(ctx.obj)
    config = services.storages.get(name)
    services.reporter.step(f"Testing {name}")
    services.storages.backend(config).test()
    services.reporter.success(f"{name} works")


@app.command("default", no_args_is_help=True)
def default(ctx: typer.Context, name: str = typer.Argument(...)) -> None:
    """Set the storage used by --upload and `outta upload`."""
    services = get_services(ctx.obj)
    services.storages.get(name)
    services.settings_store.set_value("default-storage", name)
    services.reporter.success(f"{name} is now the default storage")


@app.command("remove", no_args_is_help=True)
def remove(ctx: typer.Context, name: str = typer.Argument(...)) -> None:
    """Remove a storage, its rclone login and any stored key."""
    services = get_services(ctx.obj)
    config = services.storages.get(name)
    services.storages.backend(config).remove()
    services.storages.remove(name)
    if services.settings.default_storage == name:
        services.settings_store.set_value("default-storage", "")
    services.reporter.success(f"Removed storage {name}")
