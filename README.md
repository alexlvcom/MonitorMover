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

Download **[MonitorMover-Setup.exe](https://github.com/alexlvcom/MonitorMover/releases/latest/download/MonitorMover-Setup.exe)**
and run it.

- Installs to `%LOCALAPPDATA%\Programs\Monitor Mover` without requiring administrator privileges for installation.
- Closes the running application cleanly during upgrades and automatically restarts it.
- Automatically checks for updates on a configurable schedule (daily by default, hourly, weekly, monthly, or never).
- Preserves all your saved profiles and settings during upgrades and uninstallation.

Choose **Update** in the update offer to download and verify the installer. The
download timeout starts on that click; a timeout shows an error and lets you retry.

At launch, Monitor Mover requests Windows administrator rights so its window-move controls can reposition Task Manager and other elevated application windows.

The installer is unsigned, so Windows SmartScreen may show an unknown-publisher warning on first run. Click **More info → Run anyway**. See [Install and first run](docs/GUIDE.md#install-and-first-run) for verification details.

## Documentation

- **[User guide](docs/GUIDE.md)** — installing, profiles, the GUI, command-line use, and where your data lives.
- **[Development](docs/DEVELOPMENT.md)** — building from source and the versioning conventions.
