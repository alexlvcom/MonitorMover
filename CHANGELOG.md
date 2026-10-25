# Changelog

All notable changes to Monitor Mover are documented in this file.

## 1.3.3 - 2026-10-25

### Main changes

- The setup wizard now shows the Monitor Mover logo on its welcome, finish and page header panels.

## 1.3.2 - 2026-10-21

### Main changes

- Check for updates no longer answers "A check is already running" when a background check is in progress; it waits for that check and shows its result. The Update/Later offer no longer blocks the rest of the app, and an open offer is brought to the front instead of opening another.
- What’s New after an upgrade lists the main changes of every release since the version you had, not only the latest one.

## 1.3.1 - 2026-10-20

### Main changes

- Recheck the latest available release when Update is clicked, so an older prompt skips superseded installers. Stop if the latest release cannot be verified.
- Show What’s New with this version’s main changes once after installation or upgrade, remembered on this PC.

## 1.3.0 - 2026-10-20

### Main changes
- Redesigned every Monitor Mover window, dialog and menu with the modern look shared by all AlexLV Windows apps: flat cards and tables, toggle switches, the monitor blue from the icon as the accent, and themed title bars.
- Added Settings → General → Theme with System (default), Light and Dark. System follows the Windows light or dark mode and switches live when Windows changes; a new choice previews at once and Cancel restores the saved theme.
- Settings is now tabbed (General, Updates, Profiles, About, Diagnostics) and fits its window without scrolling. Help → About opens the About tab, and the paths and Copy details moved to Diagnostics.

### Other updates
- A taskbar pin made from an older copy of the app now opens the installed version instead.
- Check for updates in Settings → Updates shows its result in the status card instead of a pop-up: "You're running the latest version (v1.3.0)" with the check time, or why the check failed. Only a newer release opens the Update dialog. Settings opened later shows the last check's own result, with an Update button when a newer version was found.
- The main window's tables fit their columns: long titles, classes, processes and paths end with an ellipsis and show the full text in a tooltip.
- The profiles folder field is wide enough for the full path.
- Naming a profile shows an error under the field instead of silently doing nothing when the name is empty.
- The Apply Profile log wraps long lines, and the change history shows each release's notes as sections.
- Windows open already themed instead of flashing light first.
- Unexpected errors are written to errors.log next to the settings.

## 1.2.2 - 2026-10-20

### Main changes
- Settings now reports an error and stays open when it cannot save, instead of silently discarding your update schedule. Previously a failed write left no settings file, so the app kept checking only once a day.

### Other updates
- Save applies the new update schedule only after it is written to disk; Cancel, Esc, and closing the window discard changes, following the shared Save/Cancel Settings model.

## 1.2.1 - 2026-10-19

- Start the update download timeout when Update is clicked. Report timeouts and allow retrying instead of leaving the download dialog stuck.

## 1.2.0 - 2026-10-17

### Main changes
- Added an Inno Setup Windows installer (`MonitorMover-Setup.exe`) targeting `%LOCALAPPDATA%\Programs\Monitor Mover` without requiring elevation, providing Start menu, desktop, and startup shortcuts.
- Added automatic graceful shutdown via the `MonitorMoverListener` window during upgrades and uninstallation, and automatic app restart after upgrading.
- Added in-app update checks against GitHub releases with configurable check frequency (hourly, daily, weekly, monthly, or never), manual check on demand, and SHA-256 checksum verification.

### Other updates
- Added a Settings dialog for configuring automatic update check intervals and opening the profiles folder.
- Redesigned the About dialog to show the running executable path, data file locations, author attribution, project link, copyable diagnostic details, and version release notes.
- Added single-instance mutex protection for the graphical interface while preserving standalone command-line executions.
- Standardized release versioning to 1.2.0 across the project file, installer, window titles, and release documentation.

## 1.1.13 - 2026-09-16

### Other updates
- The app now calls itself "Monitor Mover" everywhere you read it: the window title, the About box, and dialog captions. The one-word form stays where it is an identifier: `MonitorMover.exe`, the GitHub repository, and the `%APPDATA%\MonitorMover` profile folder.
- Updated repository links to reflect the move to `alexlvcom/MonitorMover`.

## 1.1.12 - 2026-09-10

### Main changes
- Request Windows administrator permission at launch so window move controls can reposition Task Manager and other elevated application windows.

## 1.1.11 - 2026-08-07

### Main changes
- Added a custom application icon (a monitor with a move arrow) embedded at all standard sizes from 16×16 to 256×256, used across all windows, File Explorer, Start menu, and taskbar.

## 1.1.10 - 2026-07-29

### Main changes
- Merged current layout captures into existing profiles so closed apps keep their saved monitor rules rather than being dropped.
- Preserved user-disabled rules when recapturing layouts, and listed closed apps as kept in the profile change table.
- Read layout fresh when saving rather than using the previous window list.

## 1.1.9 - 2026-07-29

### Main changes
- Profile rules now position all open windows of an executable instead of only the first instance.
- Folded profiles to store one rule per executable, cascading additional normal-state windows.
- Added color-coded comparison table showing additions, removals, and modifications when saving profiles.
- Disabled Save button and displayed an informative banner when current layout matches the saved profile.

## 1.1.8 - 2026-07-28

### Other updates
- Save Current Layout now updates the active profile directly; added "Save Current Layout as New Profile" for creating distinct profiles.
- Added a Delete button to the profile toolbar with confirmation.
- Fixed toolbar and profile-editor caption clipping and name field overlap on high-DPI displays.

## 1.1.7 - 2026-07-28

### Main changes
- Added desktop-layout position matching to distinguish between identical monitor resolutions.
- Numbered monitors by desktop layout order (left-to-right, with primary at 0,0) rather than device enum order.

## 1.1.6 - 2026-07-28

### Other updates
- Displayed application icons in the window list next to window titles.

## 1.1.5 - 2026-07-28

### Main changes
- Improved monitor matching on re-dock by matching resolution and primary state first.

## 1.1.4 - 2026-07-22

### Other updates
- Auto-sized profile-bar buttons across different DPI settings.

## 1.1.3 - 2026-07-22

### Other updates
- Adjusted default layout to give the window list majority space over the monitors pane.

## 1.1.2 - 2026-07-22

### Other updates
- Added text filtering to the window list via typing or the filter text box.

## 1.1.1 - 2026-07-22

### Main changes
- Used `SetWindowPlacement` to reliably move maximized and minimized windows across monitors with differing DPI or resolutions.

## 1.1.0 - 2026-07-22

### Main changes
- Added monitor selection and right-click actions to filter the window list by monitor.
- Added changelog history to Help → About.

## 1.0.0 - 2026-07-22

### Main changes
- Initial release: detect monitors, list windows, move windows between displays, and save/apply profile layouts.
- Added command-line options: `--list`, `--capture`, `--apply`, and `--dump`.
