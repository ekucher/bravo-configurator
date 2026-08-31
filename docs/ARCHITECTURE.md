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

  app/                GUI layer (github.com/lxn/walk). Split into:
                      - model.go: FormModel, a pure data/logic layer with
                        no walk dependency (schema -> form-field mapping,
                        edit application, save-gating) — unit-tested
                        without a real window.
                      - save.go: backup + render + atomic-write
                        orchestration for one FormModel — also
                        walk-independent, unit-tested.
                      - window.go: actual walk widget construction from a
                        FormModel. Cannot be exercised headlessly (no
                        display in CI); validated by compilation plus the
                        manual checklist in BUILDING.md.
```

Dependency direction: `cmd` -> `app` -> {`profile`, `validate`, `ini`,
`schema`, `backup`}; `validate` -> {`ini`, `schema`}; `profile` ->
`schema`. Nothing lower-level ever imports `app` or `cmd`.

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

## Data flow (GUI path)

```
operator picks profile ("bravo"/"bis")
  -> profile.Find returns Profile{Name, DisplayName, FileHint}
operator picks a file (+ optional encoding override)
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
