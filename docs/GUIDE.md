# Monitor Mover — user guide

- [Install and first run](#install-and-first-run)
- [When you need this](#when-you-need-this)
- [How profiles work](#how-profiles-work)
- [Using the window](#using-the-window)
- [Command line](#command-line)
- [Where your data lives](#where-your-data-lives)
- [Notes and limits](#notes-and-limits)

## Install and first run

Download **[MonitorMover.exe](https://github.com/alexlvcom/MonitorMover/releases/latest/download/MonitorMover.exe)**
from the [releases page](https://github.com/alexlvcom/MonitorMover/releases/latest).
It is a single self-contained executable — no .NET runtime install needed. Just
download and double-click.

### Windows SmartScreen

The exe is **unsigned** (there is no paid code-signing certificate for a free
hobby tool), so Windows may show **"Windows protected your PC."** This is *not* a
virus warning — it only means the file is new and has not built up download
reputation yet. Click **More info → Run anyway**.

Prefer to be sure? The source is right here — see [Development](DEVELOPMENT.md)
and build it yourself.

### Verifying the download

Each release lists the SHA-256 of the exe:

```powershell
Get-FileHash .\MonitorMover.exe -Algorithm SHA256
```

Compare it against the hash on that version's
[release page](https://github.com/alexlvcom/MonitorMover/releases/latest).

### Administrator rights

Monitor Mover requests administrator permission at launch so it can move Task
Manager and other elevated apps. Cancelling that prompt prevents the app from
starting.

## When you need this

- **Switching between home and work.** Your two desks have different monitor
  counts, resolutions or arrangements, and Windows scatters your apps on each
  move. Save a **Home** profile and an **Office** profile once, then apply the
  right one and every app snaps back to its screen, position, size and state.
- **After a monitor was disconnected and reconnected.** Undocking a laptop, a
  monitor sleeping, waking from standby or a bumped cable makes Windows collapse
  everything onto one screen. Apply your profile and the layout is repaired in one
  click.
- **Ad-hoc, without profiles.** You just want *this one window* on the other
  monitor — select it and send it to the next / primary / a specific monitor
  (F8 / F7).

## How profiles work

Arrange your windows once at each location, save a profile there, and each day
pick the matching profile and hit **Apply**.

### Matching monitors back

Profiles are keyed to the monitor layout at capture time and matched back on a
series of signals, each only breaking ties left by the one before it:
**resolution**, then **desktop-layout position**, then the **primary flag**, and
only as a last resort the device name and monitor index.

Position is what tells two monitors of the *same* resolution apart — the primary
monitor's top-left is always `0,0` and every other screen keeps the offset you
arranged in Display Settings. Together this survives Windows reassigning display
device names when monitors are unplugged and reconnected.

Monitors are numbered by that same layout — **#1 is always the monitor at `0,0`**,
then left-to-right — rather than by the device enumeration order Windows reshuffles
on re-dock.

### One rule per application

A profile stores **executables, not individual windows**: one rule per `.exe`, and
applying it moves **every** open window of that app to the app's monitor. Open a
second Chrome window, a third Evernote note or another PhpStorm project and they
all follow the same rule — nothing gets left behind on the screen it happened to
open on. Extra instances of a *normal* (non-maximized) window are cascaded slightly
so they do not stack exactly on top of each other.

Need one window somewhere else? Give it its own rule with a **Title Contains**
filter in the profile editor — title-narrowed rules claim their windows first, and
the app's general rule then takes all the remaining ones.

### Closing an app never drops it

Saving the current layout over an existing profile **merges** into it rather than
replacing it. A capture can only see what is open, so:

- apps that are running have their rule refreshed to where they sit now,
- apps that are **closed keep their rule** — quit Discord for a week and it still
  returns to its monitor when you next open it,
- apps you have not captured before are added, and a rule you switched off stays
  off.

Dropping an app is therefore always deliberate: select its row in the editor and
click **Remove Selected Rows**. Closed apps are listed as **"kept"** in the change
table so you can see the profile still remembers them.

### Seeing what changed before you save

Updating an existing profile — **Save Current Layout…** or **Edit…** — shows a
comparison table right in the dialog, above the Save button, so you review the
difference *before* overwriting anything:

| Change | Application | Monitor | State | Position | Size |
|---|---|---|---|---|---|
| ~ changed | Evernote.exe | #2 → #1 | Normal → Maximized | 100,80 | 900x700 |
| = kept | Discord.exe | #2 | Maximized | 195,35 | 1725x905 |
| + added | Termius.exe | #2 | Normal | 100,80 | 900x700 |

Rows are colour-coded — green added, red removed, amber changed, grey-blue kept —
and within a changed row only the fields that actually moved are highlighted; the
rest are greyed out so your eye goes straight to the difference. The table updates
live as you edit rows or rename the profile, and unchanged apps are collapsed into
a count (tick **Show unchanged apps** to list them).

If nothing differs, a blue banner reads **"✓ NO CHANGES TO SAVE"**, and the Save
button is disabled and relabelled **"Nothing to Save"** — so a profile can never be
overwritten with an identical copy. When there *are* changes the banner turns amber
and counts them.

## Using the window

Top panel = monitors, bottom panel = windows.

- **Find an app by keyword:** type into the **Filter apps** box to narrow the list
  to windows whose title or process name contains what you typed. Clear the box to
  show everything again.
- **Filter by monitor:** click a monitor in the top pane to show only that
  monitor's windows below; right-click a monitor → *Show Windows On All Monitors*
  to clear the filter.
- **Move a window:** right-click it → *Move To Next / Primary / specific Monitor*
  (or F8 / F7).
- **Update the selected layout:** click **Save Current Layout…** to recapture the
  currently selected profile without asking for another name.
- **Save a new layout:** *Profiles → Save Current Layout as New Profile…* —
  captures all open app windows under a new name, then lets you prune the list
  before saving.
- **Add just a few apps:** select windows → right-click → *Add Selected to
  Profile…*.
- **Edit a profile:** pick it in the Profile dropdown → *Edit…* — one row per
  application: toggle rows on or off, change target monitor or state, or add a
  *Title Contains* filter to split one window off from the rest.
- **Delete a profile:** pick it in the Profile dropdown → **Delete…**, then confirm.
- **Apply:** pick a profile in the dropdown → **Apply**.

## Command line

Handy for a login shortcut or scheduled task:

```
MonitorMover.exe --list                 List saved profiles
MonitorMover.exe --capture "Office"     Capture current layout as "Office" (headless)
MonitorMover.exe --apply   "Office"     Apply the "Office" profile (headless; pops a report only if something was skipped)
MonitorMover.exe --dump [file]          Diagnostic dump of detected monitors + windows
```

**Tip — one-click Home/Office:** make two desktop shortcuts, one for
`--apply "Home"` and one for `--apply "Office"`, and double-click the right one
when you sit down.

## Where your data lives

`%APPDATA%\MonitorMover\profiles.json` — human-readable; edit or back it up freely.
*File → Open Profiles Folder* jumps there.

## Notes and limits

- Matching is by process name, so **all** windows of an app share its rule. Add a
  *Title Contains* filter only when one specific window needs a different monitor
  from the rest.
- Minimized and maximized windows capture their **restore** position, so they land
  correctly when reopened.
- **Upgrading over an older copy:** Windows caches executable icons, so an existing
  taskbar pin or shortcut may keep showing the old icon after replacing the exe.
  Unpin and re-pin it once — or run `ie4uinit.exe -show` — to refresh the cache.
