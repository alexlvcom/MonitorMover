# Monitor Mover

Move application windows between monitors, and save your window layout as a named
profile you can re-apply in one click.

Windows is bad at remembering where your windows belong when the display setup
changes. Dock at the office, undock at home, wake a monitor from sleep — and
everything piles onto one screen. Save an **Office** profile and a **Home**
profile once, then apply the right one when you sit down.

![Monitor Mover main window](MonitorMover-screenshot.jpg)

## Features

- **Move a window** to the next monitor, the primary monitor, or a specific one.
- **Layout profiles** — capture every open app's monitor, position, size and state, and restore it later.
- **Survives re-docking** — profiles match monitors on resolution and desktop position, not the device names Windows reshuffles.
- **Filter apps by keyword** — type part of a window title or process name to find it instantly.
- **Review before you save** — a colour-coded table shows exactly what moved before a profile is overwritten.
- **Command line** — `--apply "Office"` in a shortcut or scheduled task.

## Install

Download **[MonitorMover.exe](https://github.com/alexlvcom/MonitorMover/releases/latest/download/MonitorMover.exe)**
and double-click it. It is a single self-contained executable — no .NET runtime
needed, no installer.

It asks for administrator rights at launch so it can move Task Manager and other
elevated windows.

The exe is unsigned, so Windows may show a SmartScreen warning on first run.
See [Install and first run](docs/GUIDE.md#install-and-first-run) for what that
means and how to verify the download.

## Documentation

- **[User guide](docs/GUIDE.md)** — installing, profiles, the GUI, command-line use, and where your data lives.
- **[Development](docs/DEVELOPMENT.md)** — building from source and the versioning conventions.
