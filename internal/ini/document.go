package ini

import "strings"

// EntryKind distinguishes what kind of physical line an Entry represents.
type EntryKind int

const (
	// KindKeyValue is a parsed "Key=Value" line.
	KindKeyValue EntryKind = iota
	// KindBlank is a preserved blank line (whitespace-only or empty).
	KindBlank
	// KindRaw is a line the parser could not classify as blank, a comment,
	// a "[Section]" header, or "Key=Value" — kept verbatim so nothing the
	// tool doesn't understand is ever silently dropped on save.
	KindRaw
)

// Entry represents one physical line inside a Section (a key/value pair, a
// blank line, or an unclassified raw line), plus any comment lines that
// preceded it in the source.
type Entry struct {
	Kind  EntryKind
	Key   string // set only for KindKeyValue; original casing as first parsed
	Value string // set only for KindKeyValue

	// LeadingComments holds full original comment lines (including the
	// prefix character and original indentation) immediately preceding
	// this entry in the source file.
	LeadingComments []string
	// InlineComment holds a trailing "; ..." (prefix included) found on the
	// same line as a KindKeyValue entry, split off only when the prefix
	// character is preceded by whitespace (see splitInlineComment). This is
	// a parser convenience beyond confirmed production behavior (the
	// verified BRAVO.Discovery reader treats the whole remainder as the
	// value) so that editing a field does not clobber a trailing comment.
	InlineComment string

	// OriginalLine is the exact original source line (no line terminator).
	// Write() re-emits it verbatim while Dirty is false, guaranteeing
	// byte-identical round-trip for anything the caller did not change.
	OriginalLine string
	// Dirty is set by Document.Set when a KindKeyValue entry's value is
	// changed, forcing Write() to regenerate "Key=Value[ ; comment]" for
	// this line instead of reusing OriginalLine.
	Dirty bool
}

// Section is one physical "[Name]" block in the source file (or, for
// Name == "", the implicit global block preceding the first header — this
// always exists, mirroring ConvertFrom-BRAVOIniFile's `$result[""] = @{}`).
// A section name may appear more than once in a file as separate physical
// Section blocks; Document.Get/Set treat all blocks sharing a name as one
// logical section (see matchingSections), matching the confirmed reader's
// single-hashtable-per-name merge behavior, while Write keeps each block in
// its original file position.
type Section struct {
	Name            string
	LeadingComments []string
	// OriginalHeader is the exact original "[Name]" source line. Empty for
	// the implicit global section and for sections created by Set/EnsureSection
	// that did not exist in the source file.
	OriginalHeader string
	Entries        []*Entry
}

// Keys returns all KindKeyValue keys in this section, in file order
// (duplicates included).
func (s *Section) Keys() []string {
	var out []string
	for _, e := range s.Entries {
		if e.Kind == KindKeyValue {
			out = append(out, e.Key)
		}
	}
	return out
}

// Document is a parsed INI file: an ordered list of physical Sections plus
// any comments trailing the last entry, together with enough metadata
// (LineEnding, TrailingNewline, and the comparison rules from ParseOptions)
// to reproduce the source exactly via Write when nothing was edited.
type Document struct {
	Sections         []*Section
	TrailingComments []string

	// LineEnding is "\r\n" or "\n", detected from the source. Mixed line
	// endings in the source are normalized to this single value on Write —
	// a deliberate simplification, documented here rather than silently.
	LineEnding string
	// TrailingNewline records whether the source file's last line ended
	// with a line terminator, so Write reproduces that exactly.
	TrailingNewline bool

	caseInsensitive bool
	dupPolicy       DuplicateKeyPolicy
}

// KeyValue is a flattened (section, key, value) triple, used by callers
// (e.g. the validation engine) that need to inspect every key physically
// present rather than only the Get-resolved winner.
type KeyValue struct {
	Section string
	Key     string
	Value   string
}

// AllEntries returns every KindKeyValue (section, key, value) triple in the
// document, in file order, including duplicates across repeated
// "[Section]" blocks.
func (d *Document) AllEntries() []KeyValue {
	var out []KeyValue
	for _, sec := range d.Sections {
		for _, e := range sec.Entries {
			if e.Kind == KindKeyValue {
				out = append(out, KeyValue{Section: sec.Name, Key: e.Key, Value: e.Value})
			}
		}
	}
	return out
}

// LogicalSectionNames returns the distinct section names in the document,
// in the order each was first seen, merging repeated physical blocks that
// share a name (per the document's case-sensitivity rule) into one entry —
// matching Get's read-time merge semantics.
func (d *Document) LogicalSectionNames() []string {
	seen := map[string]bool{}
	var out []string
	for _, s := range d.Sections {
		norm := d.normalize(s.Name)
		if seen[norm] {
			continue
		}
		seen[norm] = true
		out = append(out, s.Name)
	}
	return out
}

func (d *Document) normalize(s string) string {
	if d.caseInsensitive {
		return strings.ToLower(s)
	}
	return s
}

func (d *Document) sectionEquals(a, b string) bool {
	return d.normalize(a) == d.normalize(b)
}

func (d *Document) keyEquals(a, b string) bool {
	return d.normalize(a) == d.normalize(b)
}

// matchingSections returns every physical Section block whose name matches
// name per the document's comparison rule, in file order.
func (d *Document) matchingSections(name string) []*Section {
	var out []*Section
	for _, s := range d.Sections {
		if d.sectionEquals(s.Name, name) {
			out = append(out, s)
		}
	}
	return out
}

// findEntry resolves the effective entry for (section, key) across every
// physical block sharing that section name, honoring the document's
// DuplicateKeyPolicy the same way for every duplicate — whether the
// duplicate is two keys in one block or the same key repeated across two
// separate "[Section]" blocks with the same name.
func (d *Document) findEntry(section, key string) (*Entry, *Section, bool) {
	var match *Entry
	var matchSec *Section
	for _, sec := range d.matchingSections(section) {
		for _, e := range sec.Entries {
			if e.Kind != KindKeyValue || !d.keyEquals(e.Key, key) {
				continue
			}
			match = e
			matchSec = sec
			if d.dupPolicy == FirstWins {
				return match, matchSec, true
			}
		}
	}
	if match == nil {
		return nil, nil, false
	}
	return match, matchSec, true
}

// Get returns the effective value for section/key, resolving duplicates
// per the document's DuplicateKeyPolicy. ok is false if the section or key
// is absent.
func (d *Document) Get(section, key string) (value string, ok bool) {
	e, _, found := d.findEntry(section, key)
	if !found {
		return "", false
	}
	return e.Value, true
}

// EnsureSection returns the first physical block named name, creating a
// new (initially empty, header-less) block appended at the end of the
// document if none exists yet.
func (d *Document) EnsureSection(name string) *Section {
	if secs := d.matchingSections(name); len(secs) > 0 {
		return secs[0]
	}
	sec := &Section{Name: name}
	d.Sections = append(d.Sections, sec)
	return sec
}

// Set writes value for section/key. If the key already exists (in any
// physical block sharing the section name), the entry Get would return is
// updated in place and marked Dirty so Write regenerates that one line;
// every other line in the file is left byte-for-byte untouched. If the key
// does not exist yet, it is appended to the last physical block with that
// section name (creating the section, at the end of the document, if none
// exists at all).
func (d *Document) Set(section, key, value string) {
	if e, _, found := d.findEntry(section, key); found {
		e.Value = value
		e.Dirty = true
		return
	}
	secs := d.matchingSections(section)
	var target *Section
	if len(secs) > 0 {
		target = secs[len(secs)-1]
	} else {
		target = d.EnsureSection(section)
	}
	target.Entries = append(target.Entries, &Entry{Kind: KindKeyValue, Key: key, Value: value, Dirty: true})
}
