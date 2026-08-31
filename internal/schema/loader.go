package schema

import (
	"embed"
	"fmt"
	"os"

	"gopkg.in/yaml.v3"
)

// embeddedSchemas bundles the DRAFT default schemas into the compiled
// binary via go:embed, so the tool is self-contained (no external files
// required at runtime) while still allowing an operator to load a
// corrected/real schema from disk via Load, without recompiling.
//
//go:embed defaults/bravo.schema.yaml defaults/bis.schema.yaml
var embeddedSchemas embed.FS

// LoadEmbedded loads the bundled default schema for profile ("bravo" or
// "bis").
func LoadEmbedded(profile string) (*Schema, error) {
	path := fmt.Sprintf("defaults/%s.schema.yaml", profile)
	data, err := embeddedSchemas.ReadFile(path)
	if err != nil {
		return nil, fmt.Errorf("schema: no bundled schema for profile %q: %w", profile, err)
	}
	return parse(data)
}

// Load reads and validates a schema YAML file from disk — used when an
// operator supplies a corrected/real schema without rebuilding the tool.
func Load(path string) (*Schema, error) {
	data, err := os.ReadFile(path)
	if err != nil {
		return nil, fmt.Errorf("schema: read %s: %w", path, err)
	}
	return parse(data)
}

func parse(data []byte) (*Schema, error) {
	var s Schema
	if err := yaml.Unmarshal(data, &s); err != nil {
		return nil, fmt.Errorf("schema: parse YAML: %w", err)
	}
	if err := validateSchemaShape(&s); err != nil {
		return nil, err
	}
	return &s, nil
}

// validateSchemaShape checks the schema document itself is well-formed
// (distinct from internal/validate, which checks an ini.Document against an
// already-loaded Schema): every field has a key/type, every rule kind is
// recognized, enum/range/regex rules carry the data they need.
func validateSchemaShape(s *Schema) error {
	if s.ProfileName == "" {
		return fmt.Errorf("schema: missing required top-level \"profile\"")
	}
	if s.Status != StatusDraft && s.Status != StatusVerified {
		return fmt.Errorf("schema %s: invalid status %q (must be %q or %q)", s.ProfileName, s.Status, StatusDraft, StatusVerified)
	}
	for _, sec := range s.Sections {
		if sec.Name == "" {
			return fmt.Errorf("schema %s: a section is missing \"name\"", s.ProfileName)
		}
		for _, f := range sec.Fields {
			if err := validateFieldShape(s.ProfileName, sec.Name, &f); err != nil {
				return err
			}
		}
	}
	return nil
}

func validateFieldShape(profile, section string, f *FieldDef) error {
	if f.Key == "" {
		return fmt.Errorf("schema %s: section %q has a field with no \"key\"", profile, section)
	}
	switch f.Type {
	case TypeString, TypePath, TypeEnum, TypeInt, TypeFloat, TypeBool:
	default:
		return fmt.Errorf("schema %s: %s.%s: invalid type %q", profile, section, f.Key, f.Type)
	}
	if f.Validation == nil {
		return nil
	}
	v := f.Validation
	switch v.Kind {
	case RulePathExists:
		switch v.PathMode {
		case "", PathModeFile, PathModeDir, PathModeEither:
		default:
			return fmt.Errorf("schema %s: %s.%s: invalid path_mode %q", profile, section, f.Key, v.PathMode)
		}
	case RuleRegex:
		if v.Pattern == "" {
			return fmt.Errorf("schema %s: %s.%s: regex rule requires \"pattern\"", profile, section, f.Key)
		}
	case RuleEnum:
		if len(v.Values) == 0 {
			return fmt.Errorf("schema %s: %s.%s: enum rule requires \"values\"", profile, section, f.Key)
		}
	case RuleRange:
		if v.Min == nil && v.Max == nil {
			return fmt.Errorf("schema %s: %s.%s: range rule requires \"min\" and/or \"max\"", profile, section, f.Key)
		}
	default:
		return fmt.Errorf("schema %s: %s.%s: invalid validation kind %q", profile, section, f.Key, v.Kind)
	}
	if v.Severity != "" && v.Severity != SeverityError && v.Severity != SeverityWarning {
		return fmt.Errorf("schema %s: %s.%s: invalid severity %q", profile, section, f.Key, v.Severity)
	}
	return nil
}
