# Schema status

The field catalogs in `src/BravoBisConfigurator.Core/Schema/Defaults/bravo.schema.yaml` and
`src/BravoBisConfigurator.Core/Schema/Defaults/bis.schema.yaml` drive both the validation engine
and the GUI form generator. This document says exactly what evidence they
are and aren't based on, so nobody mistakes "the tool didn't complain"
for "this file is definitely correct."

## What the schemas are based on

Both schemas were built from **one real production-shaped sample of each
file**, supplied directly by the tool's operator (not fetched from any
vendor documentation — none was found). The sample files themselves are
not committed to this repository (see below); the schema YAML files
describe their *shape* (sections, keys, inferred types) without embedding
their actual production values.

Key facts this evidence does and does not establish:

- **Which keys exist, in which sections, and roughly what type they hold**
  — established directly from the sample (a key that appears with a
  numeric value is typed `int`; a value that's clearly a filesystem path
  is typed `path`; etc).
- **Which keys were actually active (uncommented) vs. shown only as a
  commented-out example** — both files, especially `bravo.ini`, are
  heavily self-documenting: most `[system]`/`[net]` keys ship as
  commented-out examples with inline comments explaining valid values and
  defaults (e.g. `;DB_O_UPDATE=0*, DB_O_RDONLY=1, DB_O_VIRTUAL=2`). Those
  comments are the source for the enum/range values encoded in the schema.
- **What a single deployment's configuration looks like** — not what every
  deployment's must look like. A key absent from the sample might still be
  a legitimate, documented option (many are, per the file's own comments);
  a key present in the sample might be specific to that one installation.

What this evidence does **not** establish:

- An authoritative value range/enum for every field — several
  interpretations (e.g. `DBMEMFREE`, `MDJAVACLEAR`) are inferred from
  short inline comments and could be incomplete or slightly off.
- The full set of dynamically-named sections `bis.ini` can contain. The
  sample includes at least one per-dialog window-state section (observed:
  `[lims.DlgFndDirectDepart_F]` with `timer`/`columns`/`Width`/`Height`
  keys) — real installations likely accumulate many of these, one per UI
  dialog an operator has resized or moved. These are **intentionally not
  modeled** in the schema; the validation engine's "unrecognized
  section/key" warning already reports and preserves them without
  requiring an ever-growing, name-specific list here.
- A hard upper bound on numbered key families. `bis.ini`'s `[net]` section
  declares `user` through `user20` (21 slots, matching what was observed);
  `[model]` declares `model` and `model1`. Further slots on another
  installation (`user21`, `model2`, ...) will simply show up as
  unrecognized (but preserved) keys rather than causing a failure.

## Severity calibration

- `bravo.ini`'s `[model]` section (`MODEL`, `BLOG`, `BEXCH`) and `bis.ini`'s
  `[model]` `model` key use `severity: error` for their `path-exists`
  check, since these are the load-bearing storage paths BRAVO-Toolkit's own
  confirmed `bravo.ini` reader (`ConvertFrom-BRAVOIniFile` /
  `Get-BRAVOIniValue` in `modules/BRAVO.Discovery/BRAVO.Discovery.psm1`)
  also depends on.
- Every other inferred numeric/enum/regex rule uses `severity: warning`,
  specifically because it is calibrated from comments in a single sample,
  not an authoritative spec — a warning is surfaced but never blocks a
  save.
- Both schemas' top-level `status:` is `verified` in the sense of "derived
  from a real file, not guessed from scratch" — it is not a claim that
  every field's semantics are individually confirmed by the vendor.

## Real sample files are not committed

`example-configs/` (the two real files this schema was derived from) is
listed in `.gitignore` and must never be committed: they contain real
internal paths, usernames, and host/share names from a live deployment.
The schema YAML files describe structure and inferred semantics only —
check them for any echoed literal operational value before committing a
future revision.

## How to correct the schema

No code changes are needed to fix or extend either schema:

1. Edit `src/BravoBisConfigurator.Core/Schema/Defaults/bravo.schema.yaml` or `bis.schema.yaml`
   directly (these are compiled into the binary as embedded resources) and
   rebuild, **or**
2. Point the tool at a corrected schema file on disk without rebuilding:
   `configurator.exe --validate --profile bravo --file bravo.ini --schema
   my-corrected-bravo.schema.yaml` (the GUI's profile-selection screen has
   the same "load a custom schema" option).

When you do have a confirmed, vendor-authoritative value range for a
field, tighten its `severity` to `error` and update this document's
"what this evidence does not establish" list accordingly.
