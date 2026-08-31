package app

import (
	"testing"

	"github.com/ekucher/bravo-bis-configurator/internal/ini"
	"github.com/ekucher/bravo-bis-configurator/internal/profile"
	"github.com/ekucher/bravo-bis-configurator/internal/schema"
)

func testSchema() *schema.Schema {
	return &schema.Schema{
		ProfileName: "bravo", Status: schema.StatusVerified,
		Sections: []schema.SectionDef{{
			Name:  "model",
			Label: "Model",
			Fields: []schema.FieldDef{
				{Key: "MODEL", Label: "Model path", Type: schema.TypePath, Required: true},
				{Key: "BLOG", Label: "BLOG dir", Type: schema.TypePath, Required: true},
			},
		}},
	}
}

func TestNewFormModel_FieldCountMatchesSchema(t *testing.T) {
	doc, err := ini.Parse("[model]\nMODEL=x\n", ini.DefaultParseOptions())
	if err != nil {
		t.Fatal(err)
	}
	prof, _ := profile.Find("bravo")
	m := NewFormModel(prof, testSchema(), doc, ini.EncodingUTF8, "bravo.ini")

	if len(m.Sections) != 1 {
		t.Fatalf("expected 1 section, got %d", len(m.Sections))
	}
	if len(m.Sections[0].Fields) != 2 {
		t.Fatalf("expected 2 fields, got %d", len(m.Sections[0].Fields))
	}
}

func TestNewFormModel_CanSave_TogglesWithRequiredField(t *testing.T) {
	doc, err := ini.Parse("[model]\nMODEL=x\n", ini.DefaultParseOptions())
	if err != nil {
		t.Fatal(err)
	}
	prof, _ := profile.Find("bravo")
	m := NewFormModel(prof, testSchema(), doc, ini.EncodingUTF8, "bravo.ini")

	if m.CanSave() {
		t.Fatalf("expected CanSave=false while required BLOG is missing")
	}

	// Find the BLOG field and confirm it reports an error.
	var blogHasError bool
	for _, f := range m.Sections[0].Fields {
		if f.Key == "BLOG" {
			blogHasError = f.HasError()
		}
	}
	if !blogHasError {
		t.Fatalf("expected the BLOG field view to report HasError=true")
	}

	m.ApplyEdit("model", "BLOG", "y")
	if !m.CanSave() {
		t.Fatalf("expected CanSave=true after filling the required BLOG field")
	}
}

func TestApplyEdit_UpdatesFieldValueAndUnderlyingDocument(t *testing.T) {
	doc, err := ini.Parse("[model]\nMODEL=old\nBLOG=y\n", ini.DefaultParseOptions())
	if err != nil {
		t.Fatal(err)
	}
	prof, _ := profile.Find("bravo")
	m := NewFormModel(prof, testSchema(), doc, ini.EncodingUTF8, "bravo.ini")

	m.ApplyEdit("model", "MODEL", "new")

	if v, _ := m.Doc.Get("model", "MODEL"); v != "new" {
		t.Fatalf("underlying document not updated: got %q", v)
	}
	found := false
	for _, f := range m.Sections[0].Fields {
		if f.Key == "MODEL" {
			found = true
			if f.Value != "new" {
				t.Fatalf("FieldView.Value not refreshed: got %q", f.Value)
			}
		}
	}
	if !found {
		t.Fatalf("MODEL field not found after refresh")
	}
}

func TestUnrecognizedFindings_OnlyIncludesUnknownKeys(t *testing.T) {
	doc, err := ini.Parse("[model]\nMODEL=x\nBLOG=y\nFutureKey=z\n", ini.DefaultParseOptions())
	if err != nil {
		t.Fatal(err)
	}
	prof, _ := profile.Find("bravo")
	m := NewFormModel(prof, testSchema(), doc, ini.EncodingUTF8, "bravo.ini")

	unrec := m.UnrecognizedFindings()
	if len(unrec) != 1 || unrec[0].Key != "FutureKey" {
		t.Fatalf("expected exactly one unrecognized finding for FutureKey, got %+v", unrec)
	}
}
