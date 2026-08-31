# Architecture

## Package layout

```
cmd/configurator/    entrypoint: flag parsing, dispatches to CLI --validate
                      mode or internal/app.RunGUI()

internal/
  ini/                generic, schema-agnostic INI parser/writer.
                      Byte-for-byte round-trip for anything not edited via
                      Document.Set (comments, blank lines, unknown/raw
                      lines, key order, casing). Encoding detection
                      (UTF-8/UTF-8 BOM/legacy codepage) lives here too.

  schema/             declarative field catalog: Schema/SectionDef/FieldDef
                      /ValidationRule, loaded from YAML (bundled via
                      go:embed, or from an arbitrary disk path to override
                      without recompiling). No knowledge of ini or GUI.

  validate/           Validate(doc *ini.Document, s *schema.Schema)
                      []Result — required/type/rule checks plus
                      "unrecognized key" warnings. No I/O beyond the
                      path-exists rule's os.Stat.

  backup/             TimestampedBackup (copy-before-overwrite) and
                      AtomicWrite (temp file + rename). No knowledge of ini
                      or schema — takes raw paths/bytes.

  profile/            the two-entry registry mapping "bravo"/"bis" to a
                      display name, default filename, and bundled schema.

  locate/             resolves the two well-known on-disk locations this
                      tool auto-discovers its files from: SystemDirectory/
                      SystemBravoIniPath (OS system directory — mirrors
                      BRAVO-Toolkit's Get-BRAVOSystemDirectoryPath) and
                      ExecutableDir (the running configurator.exe's own
                      directory). No knowledge of ini/schema/app; takes
                      injectable Options for deterministic tests.

  app/                GUI layer (github.com/lxn/walk). Split into:
                      - model.go: FormModel, a pure data/logic layer with
                        no walk dependency (schema -> form-field mapping,
                        edit application, save-gating) — unit-tested
                        without a real window.
                      - discover.go: defaultPathForProfile maps a Profile
                        to its locate-resolved default path — walk-free,
                        unit-tested indirectly via save_test.go's
                        systemBravoIniPathFunc/executableDirFunc stubs.
                      - save.go: backup + render + atomic-write
                        orchestration for one FormModel, plus the
                        bravo-only "root copy" side effect (see
                        "Auto-discovery" below) — also walk-independent,
                        unit-tested.
                      - window.go: actual walk widget construction from a
                        FormModel. Cannot be exercised headlessly (no
                        display in CI); validated by compilation plus the
                        manual checklist in BUILDING.md.
```

Dependency direction: `cmd` -> `app` -> {`profile`, `validate`, `ini`,
`schema`, `backup`, `locate`}; `validate` -> {`ini`, `schema`}; `profile`
-> `schema`. Nothing lower-level ever imports `app` or `cmd`.

## Auto-discovery

The configurator ships inside the same directory as `bis.ini` on a LIMS
client install, but `bravo.ini` belongs to the LIMS server component and
always lives in the OS system directory instead (confirmed by
BRAVO-Toolkit's own `ConvertFrom-BRAVOIniFile` reader). Rather than make
an operator browse for either file every time:

- **bis.ini**: `defaultPathForProfile` looks for `Profile.FileHint` next
  to the running executable (`locate.ExecutableDir()`).
- **bravo.ini**: `defaultPathForProfile` looks at
  `locate.SystemBravoIniPath()` — `SysWOW64` on a 64-bit OS, `System32` on
  a 32-bit OS (bravo.exe is a 32-bit service; WOW64 redirects its
  "System32" accesses, so `SysWOW64` is the one real, absolute directory
  bravo.ini lives in on a 64-bit OS). This exactly mirrors
  BRAVO-Toolkit's `Get-BRAVOSystemDirectoryPath`, so both tools agree on
  the same authoritative path.

`resolveFilePath` (window.go) tries the default path first; if it doesn't
exist there (missing `%SystemRoot%`, an unusual deployment layout,
first-time setup before bravo.ini exists yet, ...) it explains why via a
`walk.MsgBox` and falls back to the manual "open file" dialog — auto-
discovery is a shortcut, never the only way in.

After a successful save of the canonical system-directory bravo.ini (and
only then — not for a file an operator manually browsed to via the
fallback dialog, and never for bis.ini), `FormModel.Save` also mirrors the
just-saved bytes to `bravo.ini` next to the executable, so an operator
without access to browse the system directory can still see the current
content. A failure of that mirror copy is reported (`SaveResult.RootCopyErr`)
but never mistaken for a failure of the primary save — see save.go's
`rootCopyTarget`.

## Why lxn/walk instead of Fyne

The original design favored Fyne (cross-platform, and its software
renderer was expected to help on old Windows). During implementation, the
actual build environment had no C compiler (`CGO_ENABLED=0`, no
mingw-w64), and Fyne on Windows requires cgo. Rather than write GUI code
that could not be compiled or tested in that environment, the project
switched to [github.com/lxn/walk](https://github.com/lxn/walk): a pure-Go
Win32 GUI toolkit (raw syscalls via `golang.org/x/sys/windows`, no cgo).
This is Windows-only, which matches the actual target (bravo.ini/bis.ini
belong to Windows LIMS software) and let every layer of the GUI package
actually be built and, where feasible, unit-tested during development
instead of shipped unverified.

## Bugs found via manual GUI testing

`internal/app/window.go` cannot be exercised in `go test` (no headless
Win32 display), so several real bugs only reproduced once a real
`MainWindow` was constructed against the bundled schema
(`internal/app/manual_window_test.go`'s `TestManualEditorWindow`, run
with `RUN_MANUAL_GUI_TEST=1`) or the built `.exe` was driven end-to-end.

1. **`TTM_ADDTOOL failed` panic on `MainWindow`/`Dialog` construction
   (Windows 11)** — an unresolved upstream `lxn/walk` bug
   ([lxn/walk#805](https://github.com/lxn/walk/issues/805)). First fixed
   for the profile/encoding `Dialog`s by embedding a Common-Controls-v6
   application manifest as a PE resource (`cmd/configurator/rsrc.syso`;
   see `BUILDING.md`). The same failure then recurred specifically for
   the main editor `MainWindow`, opened immediately after a modal
   `Dialog` closes: `lxn/walk` caches one `ToolTip` control per
   OS-thread `WindowGroup`; when the last window of a group closes, the
   group (and its `ToolTip`) is torn down, and the next top-level window
   gets a freshly created `ToolTip` — and on this Windows 11 build, that
   fresh tooltip's first `TTM_ADDTOOL` call fails again, regardless of
   the manifest. Root-caused by adding temporary `dbg()` stderr tracing
   at each `RunGUI` step, then writing `TestManualEditorWindow` to call
   `runEditorWindow` directly so `go test` would surface the real Go
   stack trace instead of the standalone `.exe` silently vanishing.
   Fixed by vendoring `github.com/lxn/walk` into `third_party/walk/`
   (`go.mod` `replace` directive) and patching `tooltip.go`'s
   `addTool()` to treat a failing `TTM_ADDTOOL` as non-fatal (matching
   the community-documented workaround for the still-open upstream
   issue): the affected widget simply loses its hover tooltip, nothing
   else is affected.
2. **Nil pointer dereference in `setFieldStatusLabel`** — the editor
   window's `statusLabels` map was declared as `map[string]*walk.Label`
   and populated with each field's `AssignTo` target *before* `walk`
   actually assigns the real widget pointer during `Create()`. Setting a
   field's initial value via `LineEdit{Text: f.Value}` synchronously
   fires `OnTextChanged` during construction, which called `refresh()`
   and dereferenced the still-nil captured pointer. Fixed by storing the
   *address* of each local `AssignTo` variable instead
   (`map[string]**walk.Label`), so later reads always see `walk`'s
   eventual assignment, plus defensive nil-checks in `refresh()`.

Both bugs were confirmed fixed by screenshot: the full editor window now
renders all 5 tabs with correct field widgets and an
"Errors: 0 Warnings: 0" summary for a valid test document.

### A false lead worth recording: `CDERR_DIALOGFAILURE` was a test-harness artifact, not a bug

While building `resolveFilePath`'s fallback path, live testing appeared to
show a real, 100%-reproducible crash: `FileDialog.ShowOpen` failing with
`Error 65535` (`CDERR_DIALOGFAILURE`) whenever it followed a `walk.Dialog`
or `walk.MsgBox` on the same thread. Two rounds of patching
`third_party/walk/commondialogs.go` (a single retry, then several
delayed retries) did not fix it, which should have been the first sign
the diagnosis was wrong — a genuine transient OS race would not survive
four 50ms-spaced retries. Bisection with a minimal `cmd/repro` program
(since deleted) proved the real cause: **the test harness was overriding
the `SystemRoot` environment variable** to point auto-discovery at a
scratch directory for testing — but `GetOpenFileName` (the Win32 common
dialog behind `FileDialog.ShowOpen`) *itself* depends on the real
`%SystemRoot%` for its own internal resource loading. Faking it for one
test broke an unrelated Windows subsystem. With the real environment
restored, the exact same code (`Dialog` → `locate.ExecutableDir()` →
`ShowOpen`) worked every time. No application code changed as a result —
`window.go`'s `resolveFilePath` and `third_party/walk` both ended up
identical to before this investigation. Lesson for next time: if
`%SystemRoot%`, `%windir%`, or another OS-meaningful environment variable
ever needs overriding for a `locate`-style test, do it only around calls
that exercise the *tool's own* lookup logic, never around a live
`FileDialog`/`MsgBox`/common-dialog call — those need the real OS
environment to function at all.

## Data flow (GUI path)

```
operator picks profile ("bravo"/"bis")
  -> profile.Find returns Profile{Name, DisplayName, FileHint}
operator picks a file (text encoding is always auto-detected; no GUI
override — pass --encoding on the CLI --validate path to force one)
  -> ini.ReadFile(path, opts, forceEncoding) -> (*ini.Document, ini.Encoding)
  -> profile.LoadSchema() / schema.Load(customPath) -> *schema.Schema
  -> app.NewFormModel(profile, schema, doc, encoding, path) -> *FormModel
     (runs validate.Validate internally, builds per-field FieldView list)
operator edits a field in the generated form
  -> FormModel.ApplyEdit(section, key, value)
     -> doc.Set(section, key, value)   // internal/ini: preserves everything else
     -> re-runs validate.Validate      // recomputes all findings
     -> rebuilds FieldView list        // GUI re-renders status labels
operator clicks Save (disabled while any SeverityError finding exists)
  -> FormModel.Save()
     -> backup.TimestampedBackup(path)   // copy-before-overwrite, skipped if new file
     -> ini.RenderFile(doc, encoding)     // Write() + re-encode
     -> backup.AtomicWrite(path, bytes)   // temp file + rename
```

## Data flow (CLI `--validate` path)

Identical to the GUI path up through `validate.Validate`, minus
`app`/`walk` entirely: `cmd/configurator/main.go`'s `runValidate` calls
`ini.ReadFile` + `schema.Load`/`LoadEmbedded` + `validate.Validate`
directly and prints the results, exiting non-zero only when a
`SeverityError` finding is present.
