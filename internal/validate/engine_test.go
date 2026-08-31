package validate

import (
	"os"
	"path/filepath"
	"testing"

	"github.com/ekucher/bravo-bis-configurator/internal/ini"
	"github.com/ekucher/bravo-bis-configurator/internal/schema"
)

func parseDoc(t *testing.T, content string) *ini.Document {
	t.Helper()
	doc, err := ini.Parse(content, ini.DefaultParseOptions())
	if err != nil {
		t.Fatal(err)
	}
	return doc
}

func floatPtr(f float64) *float64 { return &f }

func TestValidate_RequiredFieldMissing(t *testing.T) {
	s := &schema.Schema{
		ProfileName: "t", Status: schema.StatusDraft,
		Sections: []schema.SectionDef{{
			Name: "model",
			Fields: []schema.FieldDef{
				{Key: "MODEL", Type: schema.TypeString, Required: true},
			},
		}},
	}
	doc := parseDoc(t, "[model]\nOTHER=x\n")
	results := Validate(doc, s)
	if !HasErrors(results) {
		t.Fatalf("expected an error result for missing required field, got %+v", results)
	}
	errCount := 0
	for _, r := range results {
		if r.Severity == schema.SeverityError {
			errCount++
			if r.Key != "MODEL" {
				t.Fatalf("unexpected error for key %q", r.Key)
			}
		}
	}
	if errCount != 1 {
		t.Fatalf("expected exactly one error result, got %d: %+v", errCount, results)
	}
}

func TestValidate_RequiredFieldPresent_NoError(t *testing.T) {
	s := &schema.Schema{
		ProfileName: "t", Status: schema.StatusDraft,
		Sections: []schema.SectionDef{{
			Name:   "model",
			Fields: []schema.FieldDef{{Key: "MODEL", Type: schema.TypeString, Required: true}},
		}},
	}
	doc := parseDoc(t, "[model]\nMODEL=x\n")
	results := Validate(doc, s)
	if HasErrors(results) {
		t.Fatalf("expected no errors, got %+v", results)
	}
}

func TestValidate_PathExists_ValidAndInvalid(t *testing.T) {
	dir := t.TempDir()
	file := filepath.Join(dir, "f.txt")
	if err := os.WriteFile(file, []byte("x"), 0o644); err != nil {
		t.Fatal(err)
	}

	fieldSchema := func(mode schema.PathMode) *schema.Schema {
		return &schema.Schema{
			ProfileName: "t", Status: schema.StatusDraft,
			Sections: []schema.SectionDef{{
				Name: "model",
				Fields: []schema.FieldDef{{
					Key: "P", Type: schema.TypePath,
					Validation: &schema.ValidationRule{Kind: schema.RulePathExists, PathMode: mode},
				}},
			}},
		}
	}

	t.Run("valid dir", func(t *testing.T) {
		doc := parseDoc(t, "[model]\nP="+dir+"\n")
		if results := Validate(doc, fieldSchema(schema.PathModeDir)); HasErrors(results) {
			t.Fatalf("expected no errors, got %+v", results)
		}
	})
	t.Run("invalid: file expected but is dir", func(t *testing.T) {
		doc := parseDoc(t, "[model]\nP="+dir+"\n")
		if results := Validate(doc, fieldSchema(schema.PathModeFile)); !HasErrors(results) {
			t.Fatalf("expected an error (dir where file expected)")
		}
	})
	t.Run("invalid: does not exist", func(t *testing.T) {
		doc := parseDoc(t, "[model]\nP="+filepath.Join(dir, "nope")+"\n")
		if results := Validate(doc, fieldSchema(schema.PathModeEither)); !HasErrors(results) {
			t.Fatalf("expected an error (nonexistent path)")
		}
	})
}

func TestValidate_Regex_ValidAndInvalid(t *testing.T) {
	s := &schema.Schema{
		ProfileName: "t", Status: schema.StatusDraft,
		Sections: []schema.SectionDef{{
			Name: "scan",
			Fields: []schema.FieldDef{{
				Key: "ext", Type: schema.TypeString,
				Validation: &schema.ValidationRule{Kind: schema.RuleRegex, Pattern: `^\.[A-Za-z0-9]+$`},
			}},
		}},
	}
	if results := Validate(parseDoc(t, "[scan]\next=.jpg\n"), s); HasErrors(results) {
		t.Fatalf("expected no errors for valid regex match, got %+v", results)
	}
	if results := Validate(parseDoc(t, "[scan]\next=jpg\n"), s); !HasErrors(results) {
		t.Fatalf("expected an error for regex mismatch")
	}
}

func TestValidate_Enum_ValidAndInvalid(t *testing.T) {
	s := &schema.Schema{
		ProfileName: "t", Status: schema.StatusDraft,
		Sections: []schema.SectionDef{{
			Name: "config",
			Fields: []schema.FieldDef{{
				Key: "checkApp", Type: schema.TypeEnum,
				Validation: &schema.ValidationRule{Kind: schema.RuleEnum, Values: []string{"on", "off"}},
			}},
		}},
	}
	if results := Validate(parseDoc(t, "[config]\ncheckApp=off\n"), s); HasErrors(results) {
		t.Fatalf("expected no errors for valid enum value, got %+v", results)
	}
	if results := Validate(parseDoc(t, "[config]\ncheckApp=maybe\n"), s); !HasErrors(results) {
		t.Fatalf("expected an error for invalid enum value")
	}
}

func TestValidate_Range_ValidAndInvalid(t *testing.T) {
	s := &schema.Schema{
		ProfileName: "t", Status: schema.StatusDraft,
		Sections: []schema.SectionDef{{
			Name: "net",
			Fields: []schema.FieldDef{{
				Key: "PORT", Type: schema.TypeInt,
				Validation: &schema.ValidationRule{Kind: schema.RuleRange, Min: floatPtr(1), Max: floatPtr(65535)},
			}},
		}},
	}
	if results := Validate(parseDoc(t, "[net]\nPORT=9001\n"), s); HasErrors(results) {
		t.Fatalf("expected no errors for in-range value, got %+v", results)
	}
	if results := Validate(parseDoc(t, "[net]\nPORT=70000\n"), s); !HasErrors(results) {
		t.Fatalf("expected an error for out-of-range value")
	}
}

func TestValidate_WarningSeverityDoesNotBlock(t *testing.T) {
	s := &schema.Schema{
		ProfileName: "t", Status: schema.StatusDraft,
		Sections: []schema.SectionDef{{
			Name: "net",
			Fields: []schema.FieldDef{{
				Key: "PORT", Type: schema.TypeInt,
				Validation: &schema.ValidationRule{Kind: schema.RuleRange, Min: floatPtr(1), Max: floatPtr(65535), Severity: schema.SeverityWarning},
			}},
		}},
	}
	results := Validate(parseDoc(t, "[net]\nPORT=70000\n"), s)
	if HasErrors(results) {
		t.Fatalf("a rule with severity=warning must never produce HasErrors=true, got %+v", results)
	}
	if len(results) != 1 || results[0].Severity != schema.SeverityWarning {
		t.Fatalf("expected exactly one warning result, got %+v", results)
	}
}

func TestValidate_UnknownKey_WarningOnly(t *testing.T) {
	s := &schema.Schema{
		ProfileName: "t", Status: schema.StatusDraft,
		Sections: []schema.SectionDef{{Name: "model", Fields: []schema.FieldDef{{Key: "MODEL", Type: schema.TypeString}}}},
	}
	doc := parseDoc(t, "[model]\nMODEL=x\nFutureKey=y\n")
	results := Validate(doc, s)
	if HasErrors(results) {
		t.Fatalf("unknown key must not produce an error, got %+v", results)
	}
	found := false
	for _, r := range results {
		if r.Key == "FutureKey" && r.Severity == schema.SeverityWarning {
			found = true
		}
	}
	if !found {
		t.Fatalf("expected an unrecognized-key warning for FutureKey, got %+v", results)
	}
}

func TestValidate_UnknownKey_DeduplicatedAcrossDuplicateOccurrences(t *testing.T) {
	s := &schema.Schema{ProfileName: "t", Status: schema.StatusDraft}
	doc := parseDoc(t, "[model]\nFutureKey=a\nFutureKey=b\n")
	results := Validate(doc, s)
	count := 0
	for _, r := range results {
		if r.Key == "FutureKey" {
			count++
		}
	}
	if count != 1 {
		t.Fatalf("expected exactly one deduplicated warning for a duplicated unknown key, got %d: %+v", count, results)
	}
}

func TestValidate_TypeCoercion_InvalidIntBoolFloat(t *testing.T) {
	s := &schema.Schema{
		ProfileName: "t", Status: schema.StatusDraft,
		Sections: []schema.SectionDef{{
			Name: "s",
			Fields: []schema.FieldDef{
				{Key: "I", Type: schema.TypeInt},
				{Key: "B", Type: schema.TypeBool},
				{Key: "F", Type: schema.TypeFloat},
			},
		}},
	}
	doc := parseDoc(t, "[s]\nI=notanint\nB=maybe\nF=notafloat\n")
	results := Validate(doc, s)
	if len(results) != 3 {
		t.Fatalf("expected 3 type-coercion errors, got %d: %+v", len(results), results)
	}
	for _, r := range results {
		if r.Severity != schema.SeverityError {
			t.Fatalf("type coercion failures must be errors, got %+v", r)
		}
	}
}

func TestValidate_CleanRealWorldModelSchema_ZeroFindings(t *testing.T) {
	dir := t.TempDir()
	blogDir := filepath.Join(dir, "BLOG")
	if err := os.MkdirAll(blogDir, 0o755); err != nil {
		t.Fatal(err)
	}
	modelFile := filepath.Join(dir, "model")
	if err := os.WriteFile(modelFile, []byte("x"), 0o644); err != nil {
		t.Fatal(err)
	}
	bexchDir := filepath.Join(dir, "bravoexch")
	if err := os.MkdirAll(bexchDir, 0o755); err != nil {
		t.Fatal(err)
	}

	s := &schema.Schema{
		ProfileName: "bravo", Status: schema.StatusVerified,
		Sections: []schema.SectionDef{{
			Name: "model",
			Fields: []schema.FieldDef{
				{Key: "MODEL", Type: schema.TypePath, Required: true, Validation: &schema.ValidationRule{Kind: schema.RulePathExists, PathMode: schema.PathModeEither}},
				{Key: "BLOG", Type: schema.TypePath, Required: true, Validation: &schema.ValidationRule{Kind: schema.RulePathExists, PathMode: schema.PathModeDir}},
				{Key: "BEXCH", Type: schema.TypePath, Required: true, Validation: &schema.ValidationRule{Kind: schema.RulePathExists, PathMode: schema.PathModeDir}},
			},
		}},
	}
	doc := parseDoc(t, "[model]\nMODEL="+modelFile+"\nBLOG="+blogDir+"\nBEXCH="+bexchDir+"\n")
	results := Validate(doc, s)
	if len(results) != 0 {
		t.Fatalf("expected zero findings for a fully valid model section, got %+v", results)
	}
}
