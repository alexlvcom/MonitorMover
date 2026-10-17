# Monitor Mover — development

Requires the .NET 8 SDK and Inno Setup 6 (for building the installer).

## Build installer and publish

Build the Windows installer package:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/build-installer.ps1
```

Outputs:
- `dist/MonitorMover-Setup.exe` — the Inno Setup installer targeting `%LOCALAPPDATA%\Programs\Monitor Mover`.
- `dist/SHA256SUMS.txt` — SHA-256 hash of the installer.

Run the update logic test suite:

```powershell
dotnet run --project tests/Updater.Tests.csproj -c Release
```

## Project layout

Flat, one file per concern:

```
MainForm.cs               main window: monitor pane, window list, filtering, update checks
SettingsForm.cs           settings dialog: update check frequency selector, profiles folder
AboutForm.cs              about dialog: version, executable/data paths, release notes, update check
ShutdownListenerWindow.cs hidden window handling WM_CLOSE for graceful installer shutdown
UpdateService.cs          GitHub release discovery, download, and SHA-256 verification
UpdateSchedule.cs         update interval calculations (hourly, daily, weekly, monthly, never)
AppSettings.cs            user configuration persistence in %APPDATA%\MonitorMover\settings.json
ProfileEditorForm.cs      profile editor and change-comparison table
Profile.cs                profile model, capture/merge/apply, monitor matching
WindowManager.cs          window enumeration and movement
NativeMethods.cs          P/Invoke surface
Changelog.cs              embedded CHANGELOG.md reader and version accessor
Program.cs                entry point, single-instance mutex, and command-line modes
installer/                Inno Setup script (MonitorMover.iss)
scripts/                  build-installer.ps1
tests/                    updater test suite
```

## Versioning

The release version lives in `MonitorMover.csproj` and `CHANGELOG.md`, which must agree:

- `Version`, `InformationalVersion`, `FileVersion`, and `AssemblyVersion` in `MonitorMover.csproj`
- The top entry in `CHANGELOG.md`

Always use exact `N.N.N` release versions. Do not use wildcards, auto-incrementing build revisions, or commit hashes in the release version.

## Naming

The spaced name **Monitor Mover** is what the user reads: window titles, the About box, dialog captions, `<Product>`, and the README. The one-word `MonitorMover` is reserved for identifiers: the assembly, executable, namespace, mutex (`MonitorMover.Singleton`), shutdown listener (`MonitorMoverListener`), and the `%APPDATA%\MonitorMover` folder.
