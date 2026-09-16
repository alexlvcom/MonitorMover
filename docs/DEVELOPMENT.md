# Monitor Mover — development

Requires the .NET 8 SDK. Nothing else.

## Build and publish

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

Output: `publish\MonitorMover.exe` — the same single-file, self-contained Windows
x64 executable the releases ship.

## Project layout

Flat, one file per concern, no designer files:

```
MainForm.cs           main window: monitor pane, window list, filtering
ProfileEditorForm.cs  profile editor and the change-comparison table
Profile.cs            profile model, capture/merge/apply, monitor matching
WindowManager.cs      window enumeration and movement
NativeMethods.cs      P/Invoke surface
Changelog.cs          version constants and the change history shown in About
Program.cs            entry point and the command-line modes
```

## Versioning

The marketing version lives in two places that must agree:

- `<VersionPrefix>` and `<FileVersion>` in `MonitorMover.csproj`
- the newest entry at the top of `Entries` in `Changelog.cs`

Add a changelog entry whenever behaviour changes and bump both. `AssemblyVersion`
is intentionally a wildcard (`1.1.*`) with `Deterministic=false`, so the About box
shows a distinct build number per build.

## Naming

The spaced name **Monitor Mover** is what the user reads: window titles, the About
box, dialog captions, `<Product>`, the README. The one-word `MonitorMover` is
reserved for identifiers — the assembly, the executable, the namespace, and the
`%APPDATA%\MonitorMover` profile folder, which must not change or existing profiles
would be orphaned.
