// Package app is the GUI layer, built on github.com/lxn/walk (a pure-Go,
// cgo-free Win32 GUI toolkit — chosen over Fyne specifically because it
// compiles in environments without a C toolchain; see docs/ARCHITECTURE.md).
//
// The GUI logic is deliberately split into two files: model.go holds
// FormModel, a pure data/logic layer with no walk dependency, so the
// schema -> form-field mapping, edit application, and save-gating logic
// can be unit-tested without a real window/display. window.go builds the
// actual walk widgets from a FormModel and cannot be exercised headlessly,
// so it is validated by compilation plus the manual checklist in
// docs/BUILDING.md.
package app

import (
	"strings"

	"github.com/ekucher/bravo-bis-configurator/internal/ini"
	"github.com/ekucher/bravo-bis-configurator/internal/profile"
	"github.com/ekucher/bravo-bis-configurator/internal/schema"
	"github.com/ekucher/bravo-bis-configurator/internal/validate"
)

// FieldView is one schema field bound to its current value in Doc and any
// validation findings that apply specifically to it.
type FieldView struct {
	Section     string
	Key         string
	Label       string
	Description string
	Type        schema.FieldType
	Value       string
	Required    bool
	Rule        *schema.ValidationRule
	Findings    []validate.Result
}

// HasError reports whether this field has a SeverityError finding — the
// widget factory in window.go uses this to render the field's error state.
func (f FieldView) HasError() bool {
	for _, r := range f.Findings {
		if r.Severity == schema.SeverityError {
			return true
		}
	}
	return false
}

// SectionView is one schema section's fields, in schema order.
type SectionView struct {
	Name        string
	Label       string
	Description string
	Fields      []FieldView
}

// FormModel is everything one editor screen needs: which profile/schema is
// active, the live ini.Document being edited, and the section/field views
// (with findings) derived from the current state of that document.
type FormModel struct {
	Profile  profile.Profile
	Schema   *schema.Schema
	Doc      *ini.Document
	Encoding ini.Encoding
	FilePath string

	Sections    []SectionView
	AllFindings []validate.Result
}

// NewFormModel builds a FormModel from an already-loaded schema/document.
func NewFormModel(prof profile.Profile, s *schema.Schema, doc *ini.Document, enc ini.Encoding, filePath string) *FormModel {
	m := &FormModel{Profile: prof, Schema: s, Doc: doc, Encoding: enc, FilePath: filePath}
	m.refresh()
	return m
}

// refresh recomputes AllFindings and rebuilds Sections from the current
// state of Doc. Called once at construction and again after every edit.
func (m *FormModel) refresh() {
	m.AllFindings = validate.Validate(m.Doc, m.Schema)

	sections := make([]SectionView, 0, len(m.Schema.Sections))
	for _, sec := range m.Schema.Sections {
		fields := make([]FieldView, 0, len(sec.Fields))
		for _, f := range sec.Fields {
			value, _ := m.Doc.Get(sec.Name, f.Key)
			fields = append(fields, FieldView{
				Section:     sec.Name,
				Key:         f.Key,
				Label:       f.Label,
				Description: f.Description,
				Type:        f.Type,
				Value:       value,
				Required:    f.Required,
				Rule:        f.Validation,
				Findings:    findingsFor(m.AllFindings, sec.Name, f.Key),
			})
		}
		sections = append(sections, SectionView{Name: sec.Name, Label: sec.Label, Description: sec.Description, Fields: fields})
	}
	m.Sections = sections
}

func findingsFor(findings []validate.Result, section, key string) []validate.Result {
	var out []validate.Result
	for _, r := range findings {
		if strings.EqualFold(r.Section, section) && strings.EqualFold(r.Key, key) {
			out = append(out, r)
		}
	}
	return out
}

// ApplyEdit writes value to (section, key) in the underlying document and
// recomputes findings/section views, so the GUI can re-render inline
// errors immediately after each field change.
func (m *FormModel) ApplyEdit(section, key, value string) {
	m.Doc.Set(section, key, value)
	m.refresh()
}

// CanSave reports whether the current state has zero SeverityError
// findings. The GUI disables its Save button while this is false.
func (m *FormModel) CanSave() bool {
	return !validate.HasErrors(m.AllFindings)
}

// UnrecognizedFindings returns the subset of AllFindings for keys the
// schema doesn't define at all (as opposed to a known field failing its
// own rule) — used to render a separate "unrecognized keys" panel.
func (m *FormModel) UnrecognizedFindings() []validate.Result {
	var out []validate.Result
	for _, r := range m.AllFindings {
		if _, _, ok := m.Schema.FindField(r.Section, r.Key); !ok {
			out = append(out, r)
		}
	}
	return out
}
