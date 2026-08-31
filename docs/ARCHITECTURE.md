# Architecture

## Project layout

```
src/BravoBisConfigurator.Core/   net8.0 class library, no WinForms dependency
  Ini/          generic, schema-agnostic INI parser/writer.
                Byte-for-byte round-trip for anything not edited via
                Document.Set (comments, blank lines, unknown/raw lines,
                key order, casing). Encoding detection (UTF-8/UTF-8 BOM/
                legacy codepage via System.Text.Encoding.CodePages)
                lives here too.

  Schema/       declarative field catalog: Schema/SectionDef/FieldDef/
                ValidationRule, deserialized with YamlDotNet from YAML
                embedded as a resource (Loader.LoadEmbedded), or loaded
                from an arbitrary disk path to override without
                recompiling (Loader.Load). No knowledge of Ini or the GUI.

  Validate/     Engine.Validate(Document doc, Schema s) -> IReadOnlyList<Result>
                — required/type/rule checks plus "unrecognized key"
                warnings. No I/O beyond the path-exists rule's File/
                Directory.Exists.

  Backup/       Atomic.TimestampedBackup (copy-before-overwrite) and
                Atomic.AtomicWrite (temp file + File.Move(overwrite: true)
                — note this explicit overwrite flag is required on .NET,
                unlike Go's os.Rename which replaces the destination by
                default). No knowledge of Ini or Schema — takes raw
                paths/bytes.

  Profile/      ProfileDefinition — the two-entry registry mapping
                "bravo"/"bis" to a display name, default filename, and
                bundled schema.

  Locate/       LocateService — resolves the two well-known on-disk
                locations this tool auto-discovers its files from:
                SystemDirectory/SystemBravoIniPath (OS system directory —
                mirrors BRAVO-Toolkit's Get-BRAVOSystemDirectoryPath) and
                ExecutableDir (AppContext.BaseDirectory). Uses
                Environment.Is64BitOperatingSystem directly — no P/Invoke
                needed, unlike the Go port's IsWow64Process fallback. No
                knowledge of Ini/Schema/App; takes injectable Options for
                deterministic tests.

  Model/        FormModel — a pure data/logic layer with no WinForms
                dependency (schema -> form-field mapping, edit
                application, save-gating), plus FormModel.Save.cs:
                backup + render + atomic-write orchestration for one
                FormModel, and the bravo-only "root copy" side effect
                (see "Auto-discovery" below). Both unit-tested without a
                real window.

src/BravoBisConfigurator.App/    net8.0-windows, UseWindowsForms=true
  Program.cs               entry point: [STAThread] Main dispatches to
                            CliRunner.Run.
  CliRunner.cs              --validate CLI mode (parity with the Go
                            version's cmd/configurator/main.go run()/
                            runValidate()) or GUI dispatch.
  GuiRunner.cs              profile pick -> file resolve (auto-discovery
                            with fallback dialog) -> EditorForm, ported
                            from the Go version's RunGUI/resolveFilePath/
                            chooseFile/openEditor.
  ProfileSelectForm.cs/
  .Designer.cs               STATIC — built in the WinForms Designer.
  EditorForm.cs/
  .Designer.cs               STATIC frame (banner, TabControl container,
                              Save/Close buttons) built in the Designer;
                              tabs/field rows are added programmatically
                              from a FormModel in the constructor
                              (BuildSectionTab/BuildFieldRow/
                              BuildEditorWidget) — the field set is
                              schema/profile-dependent and cannot be
                              pre-drawn.
```

Dependency direction: `App` -> `Core` (`Ini`, `Schema`, `Validate`,
`Backup`, `Locate`, `Profile`, `Model`); `Validate` -> {`Ini`, `Schema`};
`Profile` -> `Schema`. Nothing in `Core` ever references `App`.

## Auto-discovery

The configurator ships inside the same directory as `bis.ini` on a LIMS
client install, but `bravo.ini` belongs to the LIMS server component and
always lives in the OS system directory instead (confirmed by
BRAVO-Toolkit's own `ConvertFrom-BRAVOIniFile` reader). Rather than make
an operator browse for either file every time:

- **bis.ini**: `GuiRunner.TryResolveFilePath` (via `Model.Discover`) looks
  for `Profile.FileHint` next to the running executable
  (`LocateService.ExecutableDir()`).
- **bravo.ini**: looks at `LocateService.SystemBravoIniPath()` —
  `SysWOW64` on a 64-bit OS, `System32` on a 32-bit OS (bravo.exe is a
  32-bit service; WOW64 redirects its "System32" accesses, so
  `SysWOW64` is the one real, absolute directory bravo.ini lives in on a
  64-bit OS). This exactly mirrors BRAVO-Toolkit's
  `Get-BRAVOSystemDirectoryPath`, so both tools agree on the same
  authoritative path.

`GuiRunner.TryResolveFilePath` tries the default path first; if it
doesn't exist there (missing `%SystemRoot%`, an unusual deployment
layout, first-time setup before bravo.ini exists yet, ...) it explains
why via a `MessageBox` and falls back to the manual "open file" dialog
(`GuiRunner.TryChooseFile`) — auto-discovery is a shortcut, never the
only way in.

After a successful save of the canonical system-directory bravo.ini (and
only then — not for a file an operator manually browsed to via the
fallback dialog, and never for bis.ini), `FormModel.Save` also mirrors the
just-saved bytes to `bravo.ini` next to the executable, so an operator
without access to browse the system directory can still see the current
content. A failure of that mirror copy is reported (`SaveResult.RootCopyError`)
but never mistaken for a failure of the primary save — see
`FormModel.Save.cs`'s `TryRootCopyTarget`.

## Why C# WinForms instead of Go/lxn-walk

The tool was originally built in Go with `github.com/lxn/walk` (a pure-Go
Win32 GUI toolkit), which worked correctly but has no visual designer —
every screen was hand-written declarative Go. When the requirement to
edit static screens (profile picker, editor frame, Save/Close) in a real
drag-and-drop designer came up, Go/`lxn-walk` had no path to that: the
project was rewritten onto C#/.NET 8 WinForms, which has a mature
Visual-Studio designer (`.Designer.cs` partial classes). Dynamically
generated content — the per-profile field rows, whose set/type/count
depend on the loaded schema — is still built programmatically at load
time in both versions; only the static chrome moved into designer files.
This is a deliberate trade-off: a larger, self-contained single-file
publish (~150 MB vs. Go's ~6 MB no-runtime-needed binary) and a
Windows 10+ minimum, in exchange for the designer workflow and a more
conventional .NET toolchain (`dotnet build`/`dotnet test`/`dotnet publish`
instead of a vendored/patched third-party GUI library).

## Data flow (GUI path)

```
operator picks profile ("bravo"/"bis")
  -> ProfileDefinition.TryFind returns ProfileDefinition{Name, DisplayName, FileHint}
operator picks a file (text encoding is always auto-detected; no GUI
override — pass --encoding on the CLI --validate path to force one)
  -> IniFile.ReadFile(path, opts, forceEncoding) -> (Document, IniEncoding)
  -> ProfileDefinition.LoadSchema() / Loader.Load(customPath) -> Schema
  -> new FormModel(profile, schema, doc, encoding, path)
     (runs Engine.Validate internally, builds per-field FieldView list)
operator edits a field in the generated form
  -> FormModel.ApplyEdit(section, key, value)
     -> doc.Set(section, key, value)   // Ini: preserves everything else
     -> re-runs Engine.Validate        // recomputes all findings
     -> rebuilds FieldView list        // GUI re-renders status labels
operator clicks Save (disabled while any Severity.Error finding exists)
  -> FormModel.Save()
     -> Atomic.TimestampedBackup(path)  // copy-before-overwrite, skipped if new file
     -> IniFile.RenderFile(doc, encoding) // Writer.Write() + re-encode
     -> Atomic.AtomicWrite(path, bytes)   // temp file + File.Move(overwrite: true)
```

## Data flow (CLI `--validate` path)

Identical to the GUI path up through `Engine.Validate`, minus
`GuiRunner`/WinForms entirely: `CliRunner.RunValidate` calls
`IniFile.ReadFile` + `Loader.Load`/`LoadEmbedded` + `Engine.Validate`
directly and prints the results, exiting non-zero only when a
`Severity.Error` finding is present (exit codes: 0 = clean/warnings-only,
1 = at least one error finding, 2 = usage/argument error).

## Behavioral parity with the Go version

This is a behavior-preserving port, not a redesign. All Core-layer logic
(INI round-trip, encoding detection/fallback, schema loading/validation
rules, atomic backup/write, auto-discovery paths, install-root/
resolved-hint computation, root-copy-on-save gating) was ported 1:1 from
the Go version's `internal/*` packages, each with a mirrored xUnit test
suite. The one deliberate, documented behavioral difference is
`File.Move`'s explicit `overwrite: true` requirement (see `Backup/` above)
— everything else was verified to match, including live-GUI verification
against a real production `bravo.ini` on this dev machine (values such as
`DBMEMLIMIT=0`, `BMTMAXTHREAD=8` render identically to the Go version's
earlier verification of the same file).
