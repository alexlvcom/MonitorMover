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
- About must show the actual running EXE path from `Application.ExecutablePath`, not a hard-coded installation path.

## Elevated application repositioning

- Monitor Mover requests Windows administrator permission via `app.manifest` (`requireAdministrator`) so its move controls can reposition Task Manager and other elevated application windows.

## GitHub updates

- Default to checking immediately on startup and daily while running. Settings must support hourly, daily, weekly, calendar-monthly, and never. Never disables startup and scheduled checks while leaving manual checks available. Keep checks asynchronous and prevent overlapping checks or downloads.
- Offer Update/Later for a newer stable release with `MonitorMover-Setup.exe`. Download only after Update is clicked. Verify the GitHub SHA-256 digest or published `SHA256SUMS.txt` before launching setup.
- Keep background failures quiet. Manual Check for updates lives in Settings and Help menu, reporting results to the user. Later suppresses that version for the current session; manual checks may offer it again.
- Run updater tests with `dotnet run --project tests/Updater.Tests.csproj -c Release`.

## About window layout

- Show the app icon before the app name. Keep the EXE path, profiles path, and settings path selectable for copying, but focus Close initially so diagnostic paths and release notes are not preselected.
- Include a "Copy details" button to copy diagnostic paths and release notes to the clipboard without requiring manual selection.

## Automated release identity

- Keep tag-driven GitHub Actions release publishing with `GITHUB_TOKEN`. GitHub showing `github-actions` as release author is expected and accepted by the owner. Do not add a personal token or manual publishing only to change this label. Commit authorship is separate.
- Order changelog and release notes by user impact. Use Main changes for the major features, followed by Other updates for smaller UI and wording refinements.
