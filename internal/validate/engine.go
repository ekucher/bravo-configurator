// Package validate checks a parsed ini.Document against a schema.Schema:
// required fields, type coercion, and per-field ValidationRule checks
// (path-exists/regex/enum/range), plus a warning for any key present in
// the file that the schema doesn't know about. It has no I/O of its own
// beyond the path-exists rule's os.Stat calls.
package validate

import (
	"fmt"
	"strings"

	"github.com/ekucher/bravo-bis-configurator/internal/ini"
	"github.com/ekucher/bravo-bis-configurator/internal/schema"
)

// Result is one finding: either a schema-defined field failing its
// required/type/validation check, or an unrecognized key in the file.
type Result struct {
	Section  string
	Key      string
	Severity schema.Severity
	Message  string
}

// HasErrors reports whether results contains at least one SeverityError
// finding. The GUI/CLI use this to decide whether a save/validate-only run
// should be blocked.
func HasErrors(results []Result) bool {
	for _, r := range results {
		if r.Severity == schema.SeverityError {
			return true
		}
	}
	return false
}

// Validate checks doc against s and returns every finding, schema fields
// first (in schema order), then unrecognized-key warnings (in file order).
func Validate(doc *ini.Document, s *schema.Schema) []Result {
	var results []Result

	for _, sec := range s.Sections {
		for _, f := range sec.Fields {
			results = append(results, validateField(doc, sec.Name, &f)...)
		}
	}

	results = append(results, unknownKeyWarnings(doc, s)...)

	return results
}

func validateField(doc *ini.Document, sectionName string, f *schema.FieldDef) []Result {
	value, ok := doc.Get(sectionName, f.Key)
	if !ok {
		if f.Required {
			return []Result{{
				Section:  sectionName,
				Key:      f.Key,
				Severity: schema.SeverityError,
				Message:  "required field is missing",
			}}
		}
		return nil
	}

	var results []Result

	if msg, ok := typeCoercionError(f.Type, value); ok {
		results = append(results, Result{
			Section:  sectionName,
			Key:      f.Key,
			Severity: schema.SeverityError,
			Message:  msg,
		})
		// A value that doesn't even match its declared type can't be
		// meaningfully checked against a further rule (e.g. a range rule on
		// a non-numeric string); stop here for this field.
		return results
	}

	if f.Validation != nil {
		if msg, failed := checkRule(f.Validation, value); failed {
			results = append(results, Result{
				Section:  sectionName,
				Key:      f.Key,
				Severity: f.Validation.EffectiveSeverity(),
				Message:  msg,
			})
		}
	}

	return results
}

// unknownKeyWarnings flags every key physically present in doc that the
// schema has no FieldDef for, deduplicated by (section, key) so a
// duplicated unknown key in the source file produces one warning, not one
// per physical occurrence.
func unknownKeyWarnings(doc *ini.Document, s *schema.Schema) []Result {
	seen := map[string]bool{}
	var results []Result
	for _, kv := range doc.AllEntries() {
		if _, _, ok := s.FindField(kv.Section, kv.Key); ok {
			continue
		}
		dedupKey := normalizeDedupKey(kv.Section, kv.Key)
		if seen[dedupKey] {
			continue
		}
		seen[dedupKey] = true
		results = append(results, Result{
			Section:  kv.Section,
			Key:      kv.Key,
			Severity: schema.SeverityWarning,
			Message:  "unrecognized key — preserved on save, not validated",
		})
	}
	return results
}

func normalizeDedupKey(section, key string) string {
	return fmt.Sprintf("%s\x00%s", strings.ToLower(section), strings.ToLower(key))
}
