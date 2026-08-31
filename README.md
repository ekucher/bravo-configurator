# BRAVO/BIS Configurator

A standalone GUI configurator-validator for two INI configuration files
belonging to an external LIMS system:

- **`bravo.ini`** — the server-side configuration.
- **`bis.ini`** — the client-side configuration.

This tool is independent of [BRAVO-Toolkit](https://github.com/ekucher/BRAVO-Toolkit)
(a separate operations/backup toolkit for the same LIMS environment) and
does not depend on its code. BRAVO-Toolkit only ever *reads* `bravo.ini`
(for path auto-discovery); this tool is the one that lets an operator
*edit and validate* both files through a GUI.

## What it does

- Parses `bravo.ini`/`bis.ini` with a generic INI parser that preserves
  every comment, blank line, key ordering, and any key it doesn't
  recognize — nothing you don't touch is ever rewritten or dropped.
- Validates the file against a declarative schema (required fields, types,
  path existence, enums, numeric ranges), showing errors and warnings
  inline.
- Lets you edit recognized fields through a generated form, grouped by
  section.
- Backs up the file (timestamped `.bak`) and writes atomically before
  overwriting anything.
- Also works headlessly: `configurator.exe --validate --profile bravo
  --file C:\path\bravo.ini` for scripted/CI checks, no GUI required.

## Schema status — read before trusting validation results

The bundled schemas (`internal/schema/defaults/*.schema.yaml`) were derived
from **one real sample of each file**, supplied by the tool's operator, not
from official vendor documentation. See [docs/SCHEMA_STATUS.md](docs/SCHEMA_STATUS.md)
for exactly what is and isn't verified, and how to correct the schema
against your own installation without recompiling.

## Building

See [docs/BUILDING.md](docs/BUILDING.md). Short version: this is a normal
Go module, built with the standard toolchain — no C compiler required
(the GUI uses [lxn/walk](https://github.com/lxn/walk), a pure-Go Win32
toolkit, deliberately chosen over Fyne so the whole project builds and
tests with nothing beyond `go build`/`go test`).

```
go build -ldflags="-H windowsgui -s -w" -o configurator.exe ./cmd/configurator
```

The result is a single portable `.exe` with no runtime dependencies beyond
stock Windows system DLLs.

## Architecture

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the package layout and
data flow.
