# Repository Instructions

- When starting user-visible changes after a release, set the application version to the next intended release version.
- Do not bump the version again for additional changes or testing iterations before that release. Application versions must correspond to published releases, not individual development changes.
- Keep all changes planned for the same unreleased version under one changelog heading in `CHANGELOG.md`.
- After implementing a user-visible app change, publish and deploy it to the local installation, then restart the running app so the user can test the result.
- Keep version values in `MonitorMover.csproj` in sync: `Version` and `InformationalVersion` use `N.N.N`; `FileVersion` and `AssemblyVersion` use `N.N.N.0`.
- Do not include commit hashes, source revision IDs, or other unique build identifiers in the About window version.
- Put versions in window titles in parentheses, e.g. `Monitor Mover (v1.2.0)` or `Monitor Mover Settings (v1.2.0)`, rather than after a dash.

## Installer and local deployment

- Build the Windows installer with `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/build-installer.ps1` on Windows (use the Windows path to the script from WSL). Requires .NET 8 SDK and Inno Setup 6.
- The output is `dist/MonitorMover-Setup.exe`, with `dist/SHA256SUMS.txt`. GitHub releases ship these files instead of the standalone application EXE.
- Default to a current-user installation without elevation at `%LOCALAPPDATA%\Programs\Monitor Mover`.
- The installed EXE is `%LOCALAPPDATA%\Programs\Monitor Mover\MonitorMover.exe`. For the local Windows account this is `C:\Users\Standfest\AppData\Local\Programs\Monitor Mover\MonitorMover.exe`.
- Deploy through the installer. Stop the running app first, run setup, and restart the installed EXE. Do not deploy to or restart the old source-tree `bin` copy.
- Setup and uninstall close the app through `WM_CLOSE` on `MonitorMoverListener`, then wait for the `MonitorMover.Singleton` mutex to be released. Keep this wired to the same clean exit as the file menu. Setup uses `PrepareToInstall` and restarts a previously running app after upgrade. Do not add an `AppMutex` directive that prompts before automatic shutdown.
- Preserve existing startup shortcuts by selecting the installer's `startup` task ("Start with Windows") when one already exists. Shortcuts must target the installed EXE.
- Keep user profiles and settings in `%APPDATA%\MonitorMover`; upgrades and uninstall must preserve them.
- Diagnostics must show the actual running EXE path from `Environment.ProcessPath`, not a hard-coded installation path.

## Elevated application repositioning

- Monitor Mover requests Windows administrator permission via `app.manifest` (`requireAdministrator`) so its move controls can reposition Task Manager and other elevated application windows.

## GitHub updates

- Default to checking immediately on startup and daily while running. Settings must support hourly, daily, weekly, calendar-monthly, and never. Never disables startup and scheduled checks while leaving manual checks available. Keep checks asynchronous and prevent overlapping checks or downloads.
- Offer Update/Later for a newer stable release with `MonitorMover-Setup.exe`. Download only after Update is clicked. Verify the GitHub SHA-256 digest or published `SHA256SUMS.txt` before launching setup.
- Keep background failures quiet. Manual Check for updates lives in Settings and Help menu, reporting results to the user. Later suppresses that version for the current session; manual checks may offer it again.
- Run updater tests with `dotnet run --project tests/Updater.Tests.csproj -c Release`.

## UI and themes

- Every window and dialog is WPF styled by the shared AlexLV Windows UI kit, vendored in `AlexLV.WindowsUi/`. The canonical copy is `C:\projects\AlexLV.WindowsUi` (see its README); change it there and copy it to every app, never edit the vendored copy. Tray Keeper is the kit's reference implementation.
- Every window derives from `ThemedWindow` (XAML root `ui:ThemedWindow`) so it never flashes light; use `MessageDialog`, never `MessageBox`. Do not set `InvariantGlobalization` to true: WPF text layout needs culture data. Tests guard both.
- `Ui/AppStyles.xaml` holds the menu bar, submenu, separator and data grid row/cell styles the kit does not cover yet. Fold them into the kit when another app needs them.
- The approved design is `design/ui-redesign.html`; update it before a visible redesign. Accent: `#1F5FBF` light, `#7AAAF2` dark (the icon's monitor blue). Surfaces stay neutral.
- Settings → General → Theme offers System (default), Light and Dark. System follows the Windows app mode live. A new theme previews at once; any close without Save restores the saved theme.
- Settings tabs (General, Updates, Profiles, About, Diagnostics) fit the fixed 720×620 window without vertical scrolling; move overflowing content to another tab. Help → About opens the About tab.
- Version and release notes come only from the assembly version and the embedded `CHANGELOG.md`; no version is hand-maintained in code.
- The main window keeps per-monitor (v2) DPI awareness (`app.manifest` and `ApplicationConfiguration.Initialize`) so window coordinates stay physical pixels for `WindowManager`.

## Shutdown listener

- `ShutdownListenerWindow` must create its handle explicitly (its `SetVisibleCore` keeps it hidden, so `Show()` alone creates nothing and setup finds no window) and must allow `WM_CLOSE` through UIPI with `ChangeWindowMessageFilterEx`, because the elevated app is closed by unelevated setup.

## About and Diagnostics layout

- About shows the app icon before the app name, the build, and "Release notes for vN.N.N" from `CHANGELOG.md`. Focus Save initially so release notes are not preselected.
- Diagnostics shows the EXE path (from `Environment.ProcessPath`), profiles path and settings path selectable for copying, each with Open, plus "Copy details" to copy them and the release notes to the clipboard.

## Automated release identity

- Keep tag-driven GitHub Actions release publishing with `GITHUB_TOKEN`. GitHub showing `github-actions` as release author is expected and accepted by the owner. Do not add a personal token or manual publishing only to change this label. Commit authorship is separate.
- Order changelog and release notes by user impact. Use Main changes for the major features, followed by Other updates for smaller UI and wording refinements.
