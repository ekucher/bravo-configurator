// Package schema loads the declarative field definitions that drive both
// internal/validate and the GUI form generator. Field definitions live in
// external YAML (assets/schema/bravo.schema.yaml, assets/schema/bis.schema.yaml),
// not in Go code, specifically because the real production schema of
// bravo.ini/bis.ini is not known yet (see docs/SCHEMA_STATUS.md) — swapping
// in a corrected schema later requires editing YAML only, no recompile.
package schema

import "strings"

// FieldType is the kind of value a FieldDef holds, used to pick a GUI
// widget and a type-coercion rule in internal/validate.
type FieldType string

const (
	TypeString FieldType = "string"
	TypePath   FieldType = "path"
	TypeEnum   FieldType = "enum"
	TypeInt    FieldType = "int"
	TypeFloat  FieldType = "float"
	TypeBool   FieldType = "bool"
)

// Severity controls whether a failed ValidationRule blocks saving (Error)
// or is merely surfaced to the operator (Warning). Draft/unverified fields
// use Warning so the tool stays honest about what it doesn't actually know.
type Severity string

const (
	SeverityError   Severity = "error"
	SeverityWarning Severity = "warning"
)

// RuleKind selects which check ValidationRule performs.
type RuleKind string

const (
	RulePathExists RuleKind = "path-exists"
	RuleRegex      RuleKind = "regex"
	RuleEnum       RuleKind = "enum"
	RuleRange      RuleKind = "range"
)

// PathMode narrows RulePathExists to files, directories, or either.
type PathMode string

const (
	PathModeFile   PathMode = "file"
	PathModeDir    PathMode = "dir"
	PathModeEither PathMode = "either"
)

// ValidationRule is one check attached to a FieldDef.
type ValidationRule struct {
	Kind     RuleKind `yaml:"kind"`
	Pattern  string   `yaml:"pattern,omitempty"`   // RuleRegex
	Values   []string `yaml:"values,omitempty"`    // RuleEnum
	Min      *float64 `yaml:"min,omitempty"`       // RuleRange
	Max      *float64 `yaml:"max,omitempty"`       // RuleRange
	PathMode PathMode `yaml:"path_mode,omitempty"` // RulePathExists; default PathModeEither
	// Severity defaults to SeverityError when empty. Draft/placeholder
	// fields should set this to SeverityWarning explicitly.
	Severity Severity `yaml:"severity,omitempty"`
}

// EffectiveSeverity returns r.Severity, defaulting to SeverityError.
func (r *ValidationRule) EffectiveSeverity() Severity {
	if r == nil || r.Severity == "" {
		return SeverityError
	}
	return r.Severity
}

// FieldDef describes one INI key within a SectionDef.
type FieldDef struct {
	Key         string          `yaml:"key"`
	Label       string          `yaml:"label"`
	Type        FieldType       `yaml:"type"`
	Required    bool            `yaml:"required"`
	Default     string          `yaml:"default,omitempty"`
	Description string          `yaml:"description,omitempty"`
	Validation  *ValidationRule `yaml:"validation,omitempty"`
}

// SectionDef describes one INI "[Name]" section and the fields within it.
type SectionDef struct {
	Name        string     `yaml:"name"`
	Label       string     `yaml:"label"`
	Description string     `yaml:"description,omitempty"`
	Fields      []FieldDef `yaml:"fields"`
}

// SchemaStatus is a whole-schema honesty marker, surfaced by the GUI as a
// persistent banner and used to decide default field severities.
type SchemaStatus string

const (
	StatusDraft    SchemaStatus = "draft"
	StatusVerified SchemaStatus = "verified"
)

// Schema is one profile's full field catalog, as loaded from YAML.
type Schema struct {
	ProfileName string       `yaml:"profile"`
	Version     string       `yaml:"version"`
	Status      SchemaStatus `yaml:"status"`
	Sections    []SectionDef `yaml:"sections"`
}

// FindField returns the FieldDef for (sectionName, key), matching
// case-insensitively (INI section/key names are case-insensitive per
// internal/ini's default rules).
func (s *Schema) FindField(sectionName, key string) (*FieldDef, *SectionDef, bool) {
	for i := range s.Sections {
		sec := &s.Sections[i]
		if !strings.EqualFold(sec.Name, sectionName) {
			continue
		}
		for j := range sec.Fields {
			f := &sec.Fields[j]
			if strings.EqualFold(f.Key, key) {
				return f, sec, true
			}
		}
	}
	return nil, nil, false
}
