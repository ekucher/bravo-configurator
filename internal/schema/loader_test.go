package schema

import (
	"os"
	"path/filepath"
	"strings"
	"testing"
)

func TestLoadEmbedded_BothProfiles(t *testing.T) {
	for _, profile := range []string{"bravo", "bis"} {
		s, err := LoadEmbedded(profile)
		if err != nil {
			t.Fatalf("LoadEmbedded(%q): %v", profile, err)
		}
		if s.ProfileName != profile {
			t.Fatalf("LoadEmbedded(%q).ProfileName = %q", profile, s.ProfileName)
		}
		if s.Status != StatusVerified {
			t.Fatalf("LoadEmbedded(%q).Status = %q, want %q (both bundled schemas are now derived from real sample files, see docs/SCHEMA_STATUS.md)", profile, s.Status, StatusVerified)
		}
		if len(s.Sections) == 0 {
			t.Fatalf("LoadEmbedded(%q) has no sections", profile)
		}
	}
}

func TestLoadEmbedded_UnknownProfile(t *testing.T) {
	if _, err := LoadEmbedded("nope"); err == nil {
		t.Fatalf("expected error for unknown profile")
	}
}

func TestBravoSchema_VerifiedFieldsPresentWithErrorSeverity(t *testing.T) {
	s, err := LoadEmbedded("bravo")
	if err != nil {
		t.Fatal(err)
	}
	for _, key := range []string{"MODEL", "BLOG", "BEXCH"} {
		f, _, ok := s.FindField("model", key)
		if !ok {
			t.Fatalf("verified field model.%s missing from bundled schema", key)
		}
		if f.Validation == nil || f.Validation.EffectiveSeverity() != SeverityError {
			t.Fatalf("verified field model.%s should have severity=error, got %+v", key, f.Validation)
		}
	}
	if _, _, ok := s.FindField("Debug", "FILE"); !ok {
		t.Fatalf("verified field Debug.FILE missing from bundled schema")
	}
}

func TestBisSchema_ModelFieldPresent(t *testing.T) {
	// Derived from a real sample (example-configs/bis.ini): [model] model=
	// is the client's own model-path key (distinct from bravo.ini's
	// [model] MODEL= on the server side).
	s, err := LoadEmbedded("bis")
	if err != nil {
		t.Fatal(err)
	}
	f, _, ok := s.FindField("model", "model")
	if !ok {
		t.Fatalf("field model.model missing from bundled bis schema")
	}
	if f.Type != TypePath {
		t.Fatalf("model.model type = %q, want %q", f.Type, TypePath)
	}
}

func TestLoad_DiskOverrideTakesPrecedenceOverEmbedded(t *testing.T) {
	dir := t.TempDir()
	path := filepath.Join(dir, "custom.schema.yaml")
	custom := `
profile: bravo
version: "1.0.0"
status: verified
sections:
  - name: model
    label: "Model"
    fields:
      - key: MODEL
        label: "Model"
        type: path
        required: true
        validation: { kind: path-exists, path_mode: dir, severity: error }
`
	if err := os.WriteFile(path, []byte(custom), 0o644); err != nil {
		t.Fatal(err)
	}
	s, err := Load(path)
	if err != nil {
		t.Fatal(err)
	}
	if s.Status != StatusVerified {
		t.Fatalf("Status = %q, want %q", s.Status, StatusVerified)
	}
}

func TestLoad_MalformedYAMLRejected(t *testing.T) {
	dir := t.TempDir()
	path := filepath.Join(dir, "broken.yaml")
	if err := os.WriteFile(path, []byte("profile: [unterminated"), 0o644); err != nil {
		t.Fatal(err)
	}
	if _, err := Load(path); err == nil {
		t.Fatalf("expected parse error for malformed YAML")
	}
}

func TestValidateSchemaShape_MissingProfile(t *testing.T) {
	_, err := parse([]byte("status: draft\nsections: []\n"))
	if err == nil || !strings.Contains(err.Error(), "profile") {
		t.Fatalf("expected missing-profile error, got %v", err)
	}
}

func TestValidateSchemaShape_InvalidFieldType(t *testing.T) {
	yamlDoc := `
profile: bravo
status: draft
sections:
  - name: model
    fields:
      - key: X
        type: not-a-real-type
`
	_, err := parse([]byte(yamlDoc))
	if err == nil || !strings.Contains(err.Error(), "invalid type") {
		t.Fatalf("expected invalid-type error, got %v", err)
	}
}

func TestValidateSchemaShape_EnumRuleWithoutValues(t *testing.T) {
	yamlDoc := `
profile: bravo
status: draft
sections:
  - name: model
    fields:
      - key: X
        type: enum
        validation: { kind: enum }
`
	_, err := parse([]byte(yamlDoc))
	if err == nil || !strings.Contains(err.Error(), "enum rule requires") {
		t.Fatalf("expected enum-without-values error, got %v", err)
	}
}

func TestValidateSchemaShape_RangeRuleWithoutBounds(t *testing.T) {
	yamlDoc := `
profile: bravo
status: draft
sections:
  - name: model
    fields:
      - key: X
        type: int
        validation: { kind: range }
`
	_, err := parse([]byte(yamlDoc))
	if err == nil || !strings.Contains(err.Error(), "range rule requires") {
		t.Fatalf("expected range-without-bounds error, got %v", err)
	}
}

func TestValidateSchemaShape_RegexRuleWithoutPattern(t *testing.T) {
	yamlDoc := `
profile: bravo
status: draft
sections:
  - name: model
    fields:
      - key: X
        type: string
        validation: { kind: regex }
`
	_, err := parse([]byte(yamlDoc))
	if err == nil || !strings.Contains(err.Error(), "regex rule requires") {
		t.Fatalf("expected regex-without-pattern error, got %v", err)
	}
}

func TestValidateSchemaShape_InvalidStatus(t *testing.T) {
	yamlDoc := "profile: bravo\nstatus: not-a-status\nsections: []\n"
	_, err := parse([]byte(yamlDoc))
	if err == nil || !strings.Contains(err.Error(), "invalid status") {
		t.Fatalf("expected invalid-status error, got %v", err)
	}
}
