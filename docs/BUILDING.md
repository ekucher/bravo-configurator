# Building

## Requirements

- Go (the version pinned in `go.mod`). **No C compiler is required** — the
  GUI uses `github.com/lxn/walk`, a pure-Go Win32 toolkit, specifically so
  the whole build stays `CGO_ENABLED=0`-friendly.
- Windows is the only supported build/run target: `bravo.ini`/`bis.ini`
  belong to Windows LIMS software, and `lxn/walk` only implements the
  Win32 backend. There is no cross-compiled Linux/Mac build — attempting
  `GOOS=linux`/`GOOS=darwin` will fail to compile the `internal/app`
  package.

## Required: embedded manifest (`cmd/configurator/rsrc.syso`)

`cmd/configurator/rsrc.syso` is checked into the repository and picked up
automatically by `go build` (Go links any `*.syso` file found in the main
package directory). **Do not delete it** — without it, `lxn/walk`'s
`MainWindow`/`Dialog` creation crashes on Windows 11 with `panic: TTM_ADDTOOL
failed` (a real, empirically-confirmed bug: [lxn/walk#805](https://github.com/lxn/walk/issues/805),
still open upstream as of writing). The fix is a Common-Controls-v6
application manifest — but it must be **embedded as a PE resource**, not
placed as a side-by-side `configurator.exe.manifest` file next to the
`.exe` (that was tried first and did not fix the crash; see
[lxn/walk#733](https://github.com/lxn/walk/issues/733), where the same
"solved" fix was embedding, not a sidecar file).

If you ever need to regenerate `rsrc.syso` (e.g. after changing DPI
awareness or execution-level settings in the manifest):

```sh
go install github.com/akavel/rsrc@latest
rsrc -manifest configurator.exe.manifest -o cmd/configurator/rsrc.syso
```

(`akavel/rsrc` is the module's real import path; `tc-hib/rsrc`, sometimes
referenced online as a fork, currently fails `go install` because its
`go.mod` still declares the `akavel/rsrc` module path.) Keep a
`configurator.exe.manifest` source file alongside the repo root while
regenerating, then remove it again — only the compiled `rsrc.syso` needs
to be committed; a side-by-side `.manifest` file left next to the built
`.exe` is not required and was not what fixed the crash.

## Required: vendored/patched `lxn/walk` (`third_party/walk/`)

`go.mod` has `replace github.com/lxn/walk => ./third_party/walk`. This is
a full local copy of `github.com/lxn/walk@v0.0.0-20210112085537-c389da54e794`
with one patch, in `tooltip.go`'s `addTool()`: a failing `TTM_ADDTOOL`
call is treated as non-fatal instead of returning an error. **Do not
delete `third_party/walk/` or remove the `replace` directive** — without
the patch, the editor `MainWindow` panics on Windows 11 (see
`docs/ARCHITECTURE.md`'s "Bugs found via manual GUI testing" §1). This is
a second, distinct fix from the `rsrc.syso` manifest above: the manifest
fixes `TTM_ADDTOOL` for the *first* window in a process; the vendored
patch fixes it for a *later* window whose per-thread `WindowGroup`
(and therefore `ToolTip`) was freshly recreated after a previous one
tore down — the manifest alone does not cover that case.

If `lxn/walk` is ever upgraded, re-apply the patch: diff the new
upstream `tooltip.go` against `third_party/walk/tooltip.go`, keep only
the `addTool()` change (search for `PATCHED (bravo-bis-configurator)`),
and re-copy everything else from upstream.

## Build

```sh
go build ./...              # everything, including a console-mode configurator.exe
go vet ./...
gofmt -l .                  # should print nothing for files owned by this repo;
                             # third_party/walk/** is vendored upstream source
                             # and is intentionally left unformatted/unmodified
                             # except for the one patched line noted above
go test ./...
```

## Release build (GUI subsystem, stripped, single portable exe)

```sh
go build -ldflags="-H windowsgui -s -w" -o configurator.exe ./cmd/configurator
```

- `-H windowsgui` suppresses the console window. If you need to see
  `--validate` mode's stdout/stderr output interactively, either run the
  console-mode build (omit `-H windowsgui`) or redirect output:
  `configurator.exe --validate ... > out.txt 2>&1`. Windows GUI-subsystem
  processes still work fine when invoked with args from a script — only
  interactive console output is affected.
- `-s -w` strips debug symbols, shrinking the binary.
- The result has no runtime dependencies beyond stock Windows system DLLs
  — copy the single `.exe` anywhere and run it.

## Windows compatibility

Empirically verified on Windows 11 (build 10.0.26200): the GUI launches,
the profile-selection dialog renders correctly, and the process stays
running (no crash) — this required the embedded-manifest fix above; do
not skip it. Not verified on Windows 7/8 (none was available in the build
environment). Before relying on this tool on an old-Windows deployment:

1. Confirm the pinned Go toolchain version in `go.mod` still supports
   building for your target — Go periodically drops support for building
   binaries that run on very old Windows versions in newer toolchain
   releases; if your target is older than what the current toolchain
   supports, pin to an older Go version instead.
2. `lxn/walk` uses standard Win32 common controls (no OpenGL/DirectX
   dependency the way Fyne would have had), which is the main reason it
   was expected to behave predictably across Windows versions — but this
   expectation has not been empirically verified on old hardware/OS as
   part of this work.
3. Run the manual checklist below on the target OS before deployment.

## Manual GUI checklist (not automated — requires a real desktop session)

`internal/app`'s `window.go` cannot be exercised in `go test` (no headless
Win32 display). After building, manually verify:

1. Launch `configurator.exe` with no arguments — the profile-selection
   dialog appears.
2. Pick "BRAVO (server)" — the file-open dialog appears, defaulted to
   `bravo.ini`.
3. Open a real (or the schema-shaped example) `bravo.ini` — the encoding
   dialog appears; accept "Auto-detect".
4. The editor window opens with one tab per section; confirm the `[model]`
   tab shows `MODEL`/`BLOG`/`BEXCH` with their current values.
5. Clear the `MODEL` field — confirm its status label shows a required-field
   error and the **Save** button becomes disabled.
6. Restore a value — confirm **Save** re-enables.
7. Click **Save** — confirm a `.bak` file appears next to the original with
   a timestamp, and the success dialog names its path.
8. Reopen the file — confirm your edit persisted and every field you did
   not touch (including any custom/unrecognized key) is unchanged.
9. Repeat steps 2–8 with "BIS (client)" and a `bis.ini` file.

## CI

`.github/workflows/ci.yml` runs on `windows-latest` (required: `lxn/walk`
and everything depending on it only compile on Windows) for every push
and PR against `main`: `go vet ./...`, a `gofmt -l` check that excludes
the vendored `third_party/walk/`, `go test ./...`, both the console and
release (`-H windowsgui -s -w`) builds, and uploads the built
`configurator.exe` as a workflow artifact. The manual GUI checklist
above (steps 1–2 of "Windows compatibility" cannot run in CI — no
interactive desktop session) still requires a real machine.
