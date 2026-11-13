"""How workflows tell the user what they are doing.

Workflows only talk to the `Reporter` protocol. The CLI passes a `ConsoleReporter`
(Rich), tests pass a `RecordingReporter` and check the messages.
"""

from __future__ import annotations

import enum
from collections.abc import Iterator
from contextlib import AbstractContextManager, contextmanager
from dataclasses import dataclass, field
from typing import Protocol

from rich.console import Console
from rich.markup import escape
from rich.progress import (
    BarColumn,
    DownloadColumn,
    Progress,
    TaskID,
    TaskProgressColumn,
    TextColumn,
    TransferSpeedColumn,
)


class Verbosity(enum.IntEnum):
    QUIET = 0
    NORMAL = 1
    VERBOSE = 2


class ProgressTask(Protocol):
    def update(self, completed: float, total: float | None = None) -> None: ...


class Reporter(Protocol):
    def step(self, message: str) -> None: ...
    def success(self, message: str) -> None: ...
    def warn(self, message: str) -> None: ...
    def detail(self, message: str) -> None: ...
    def info(self, message: str) -> None: ...
    def progress(
        self, description: str, total: float | None = None
    ) -> AbstractContextManager[ProgressTask]: ...


class _RichTask:
    def __init__(self, progress: Progress, task_id: TaskID) -> None:
        self._progress = progress
        self._task_id = task_id

    def update(self, completed: float, total: float | None = None) -> None:
        if total is not None:
            self._progress.update(self._task_id, completed=completed, total=total)
        else:
            self._progress.update(self._task_id, completed=completed)


class _NullTask:
    def update(self, completed: float, total: float | None = None) -> None:
        return None


class ConsoleReporter:
    """Prints arrows for steps, check marks for results and a bar for transfers."""

    def __init__(self, console: Console | None = None, verbosity: Verbosity = Verbosity.NORMAL):
        self.console = console or Console(highlight=False)
        self.verbosity = verbosity

    def step(self, message: str) -> None:
        if self.verbosity >= Verbosity.NORMAL:
            self.console.print(f"[cyan]→[/] {escape(message)}")

    def success(self, message: str) -> None:
        self.console.print(f"[green]✓[/] {escape(message)}")

    def warn(self, message: str) -> None:
        self.console.print(f"[yellow]![/] {escape(message)}")

    def detail(self, message: str) -> None:
        if self.verbosity >= Verbosity.VERBOSE:
            self.console.print(f"  [dim]{escape(message)}[/]")

    def info(self, message: str) -> None:
        self.console.print(escape(message))

    @contextmanager
    def progress(self, description: str, total: float | None = None) -> Iterator[ProgressTask]:
        if self.verbosity < Verbosity.NORMAL:
            yield _NullTask()
            return
        progress = Progress(
            TextColumn("  "),
            BarColumn(bar_width=20),
            TaskProgressColumn(),
            DownloadColumn(),
            TransferSpeedColumn(),
            console=self.console,
            transient=False,
        )
        with progress:
            task_id = progress.add_task(description, total=total)
            yield _RichTask(progress, task_id)


@dataclass
class _RecordedTask:
    updates: list[tuple[float, float | None]] = field(default_factory=list)

    def update(self, completed: float, total: float | None = None) -> None:
        self.updates.append((completed, total))


@dataclass
class RecordingReporter:
    """Keeps every message as (kind, text). Used by tests."""

    messages: list[tuple[str, str]] = field(default_factory=list)
    tasks: list[_RecordedTask] = field(default_factory=list)

    def step(self, message: str) -> None:
        self.messages.append(("step", message))

    def success(self, message: str) -> None:
        self.messages.append(("success", message))

    def warn(self, message: str) -> None:
        self.messages.append(("warn", message))

    def detail(self, message: str) -> None:
        self.messages.append(("detail", message))

    def info(self, message: str) -> None:
        self.messages.append(("info", message))

    @contextmanager
    def progress(self, description: str, total: float | None = None) -> Iterator[ProgressTask]:
        self.messages.append(("progress", description))
        task = _RecordedTask()
        self.tasks.append(task)
        yield task

    def texts(self, kind: str | None = None) -> list[str]:
        return [text for k, text in self.messages if kind is None or k == kind]

    def kinds(self) -> list[str]:
        return [k for k, _ in self.messages]
