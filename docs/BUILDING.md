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

## Build

```sh
go build ./...              # everything, including a console-mode configurator.exe
go vet ./...
gofmt -l .                  # should print nothing
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

## Old-Windows compatibility

Not verified against a real Windows 7/8 machine as part of this work (none
was available in the build environment). Before relying on this tool on an
old-Windows deployment:

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

No CI workflow is configured yet. A `windows-latest` GitHub Actions runner
executing `go vet ./...`, `gofmt -l .`, `go test ./...`, and the release
build command above (steps 1–2 above cannot run in CI; only compilation
and the walk-independent unit tests can) would be a reasonable starting
point.
