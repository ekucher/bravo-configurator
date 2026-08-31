// Package ini implements a generic, schema-agnostic INI parser/writer that
// preserves byte-for-byte round-trip fidelity for every line a caller does
// not explicitly modify (comments, blank lines, unknown/malformed lines,
// key ordering, original casing). It is used as the storage layer under
// internal/schema and internal/validate, which know nothing about INI
// syntax themselves.
package ini

// DuplicateKeyPolicy controls how Document.Get/Set resolve a key that
// appears more than once within the same logical section (including
// across repeated "[Section]" headers in the same file).
type DuplicateKeyPolicy int

const (
	// LastWins mirrors BRAVO-Toolkit's confirmed bravo.ini reader
	// (ConvertFrom-BRAVOIniFile in modules/BRAVO.Discovery/BRAVO.Discovery.psm1):
	// the last occurrence of a duplicated key wins. This is the default.
	LastWins DuplicateKeyPolicy = iota
	// FirstWins resolves duplicates to the first occurrence instead.
	FirstWins
	// ErrorOnDuplicate makes Parse fail with a *DuplicateKeyError as soon as
	// a key repeats within the same logical section, instead of silently
	// picking a winner.
	ErrorOnDuplicate
)

// ParseOptions controls Parse's syntax handling. The zero value is not
// valid on its own (CommentPrefixes would be empty); use
// DefaultParseOptions to get BRAVO-Toolkit-compatible defaults and
// override individual fields from there.
type ParseOptions struct {
	// CommentPrefixes lists the rune(s) that start a comment when they are
	// the first non-blank character on a line. Confirmed production
	// behavior (BRAVO.Discovery.psm1) uses ';' only; '#' is offered here as
	// a defensive parser capability for other INI dialects, not because it
	// has been observed in a real bravo.ini/bis.ini file.
	CommentPrefixes []rune
	// DuplicateKeyPolicy controls duplicate-key resolution; see the
	// constants above.
	DuplicateKeyPolicy DuplicateKeyPolicy
	// CaseInsensitiveKeys controls whether section names and keys are
	// compared case-insensitively for Get/Set/duplicate detection.
	// Confirmed production behavior is case-insensitive (both section
	// names, via -ieq in Get-BRAVOIniValue, and keys).
	CaseInsensitiveKeys bool
}

// DefaultParseOptions returns the options that match the confirmed
// behavior of BRAVO-Toolkit's ConvertFrom-BRAVOIniFile: ';'-only comments,
// last-duplicate-wins, case-insensitive section/key matching.
func DefaultParseOptions() ParseOptions {
	return ParseOptions{
		CommentPrefixes:     []rune{';'},
		DuplicateKeyPolicy:  LastWins,
		CaseInsensitiveKeys: true,
	}
}
