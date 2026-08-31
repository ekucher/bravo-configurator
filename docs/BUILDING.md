# Building

## Requirements

- .NET 8 SDK.
- Windows is the only supported build/run target: `bravo.ini`/`bis.ini`
  belong to Windows LIMS software, and the GUI project
  (`BravoBisConfigurator.App`) targets `net8.0-windows` with
  `UseWindowsForms=true`. The `BravoBisConfigurator.Core` library itself
  is plain `net8.0` (no WinForms dependency) and is Windows-agnostic.

## Solution layout

```
BravoBisConfigurator.sln
src/
  BravoBisConfigurator.Core/    ini/schema/validate/backup/locate/profile/model — no UI dependency
  BravoBisConfigurator.App/     net8.0-windows, WinForms GUI + CLI entry point
tests/
  BravoBisConfigurator.Core.Tests/
  BravoBisConfigurator.App.Tests/
```

## Build

```sh
dotnet build BravoBisConfigurator.sln -c Release
dotnet test BravoBisConfigurator.sln
```

`BravoBisConfigurator.App.Tests` cannot exercise the no-flags GUI path
(`GuiRunner.Run()` creates a real Win32 window and blocks on a message
loop — it needs an interactive desktop session and cannot run headlessly
in `dotnet test`). See the manual GUI checklist below instead.

## Release build (self-contained single-file exe)

```sh
dotnet publish src/BravoBisConfigurator.App/BravoBisConfigurator.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

- Produces one portable `BravoBisConfigurator.App.exe` (~150 MB) with no
  .NET runtime install required on the target machine. This is the
  accepted trade-off versus a framework-dependent build: a much larger
  binary and a minimum-supported OS of **Windows 10+**, in exchange for
  zero deployment prerequisites — an explicit, owner-approved decision
  made when the project moved off the ~6 MB no-runtime-needed Go/`lxn-walk`
  build.
- For a smaller, framework-dependent build (requires the .NET 8 Desktop
  Runtime already installed on the target machine), omit
  `--self-contained`/`PublishSingleFile`/`IncludeNativeLibrariesForSelfExtract`
  and use a plain `dotnet publish -c Release -r win-x64`.
- `--validate` mode's stdout/stderr is a normal console app output; run
  the built `.exe` directly from a terminal to see it, or redirect:
  `BravoBisConfigurator.App.exe --validate ... > out.txt 2>&1`.

## Windows compatibility

Empirically verified on Windows 11 (build 10.0.26200): the GUI launches,
the profile-selection dialog renders correctly, editing/saving both a
sample `bis.ini` and the real production `bravo.ini` on this dev machine
work correctly, and the process exits cleanly. Not verified on Windows
7/8/8.1 — those predate the .NET 8 minimum-OS baseline (Windows 10+) and
are not supported targets for this build.

## Manual GUI checklist (not automated — requires a real desktop session)

`BravoBisConfigurator.App`'s `EditorForm`/`GuiRunner` cannot be exercised
in `dotnet test` (no headless Win32 message loop for a real window).
After building, manually verify:

1. Launch the `.exe` with no arguments — the profile-selection dialog
   appears (Ukrainian UI text).
2. Pick "BRAVO (сервер)". Two cases (see ARCHITECTURE.md's
   "Auto-discovery"):
   - A real `bravo.ini` already exists in the OS system directory
     (`SysWOW64` on 64-bit Windows, `System32` on 32-bit) — the editor
     opens **directly**, no file dialog at all; confirm the banner shows
     that exact system-directory path.
   - It doesn't exist there yet — an informational dialog explains where
     it looked, then the manual "open file" dialog appears, defaulted to
     `bravo.ini`.
3. Open a real (or the schema-shaped example) `bravo.ini` via the file
   dialog (if step 2 needed one) — the editor opens directly; text
   encoding is always auto-detected, there is no GUI override (pass
   `--encoding` on the CLI `--validate` path if a legacy codepage ever
   needs to be forced).
4. The editor window opens with one tab per section; confirm the `[model]`
   tab shows `MODEL`/`BLOG`/`BEXCH` with their current values, bold field
   labels, and no clipped text.
5. Clear the `MODEL` field — confirm its status label shows a required-field
   error and the **Save** button becomes disabled.
6. Restore a value — confirm **Save** re-enables.
7. Click **Save** — confirm a `.bak` file appears next to the original with
   a timestamp, and the success dialog names its path. If this was the
   canonical system-directory `bravo.ini` (step 2's first case), also
   confirm a `bravo.ini` copy appeared/updated next to the `.exe` itself,
   identical to the just-saved system-directory file — the success dialog
   names that path too.
8. Reopen the file — confirm your edit persisted and every field you did
   not touch (including any custom/unrecognized key) is unchanged.
9. Repeat steps 2–8 with "BIS (client)" — auto-discovery looks for
   `bis.ini` next to the `.exe` itself; place one there to hit the
   direct-open case, or remove it to see the fallback dialog. No
   root-copy step applies to `bis.ini` (it's already at its own root
   location).

**When testing against a real production `bravo.ini`, never click Save —
close the window instead.**

## CI

`.github/workflows/ci.yml` runs on `windows-latest` (required: the App
project targets `net8.0-windows`/WinForms and only builds on Windows) for
every push and PR against `main`: `dotnet build`, `dotnet test`, and a
self-contained single-file `dotnet publish`, uploading the built `.exe` as
a workflow artifact. The manual GUI checklist above cannot run in CI — no
interactive desktop session — and still requires a real machine.
