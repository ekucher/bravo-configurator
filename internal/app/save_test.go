package app

import (
	"os"
	"path/filepath"
	"testing"

	"github.com/ekucher/bravo-bis-configurator/internal/ini"
	"github.com/ekucher/bravo-bis-configurator/internal/profile"
)

func TestSave_BlockedWhileErrorsRemain(t *testing.T) {
	dir := t.TempDir()
	path := filepath.Join(dir, "bravo.ini")
	if err := os.WriteFile(path, []byte("[model]\nMODEL=x\n"), 0o644); err != nil {
		t.Fatal(err)
	}
	doc, enc, err := ini.ReadFile(path, ini.DefaultParseOptions(), "")
	if err != nil {
		t.Fatal(err)
	}
	prof, _ := profile.Find("bravo")
	m := NewFormModel(prof, testSchema(), doc, enc, path) // BLOG required and missing

	if _, err := m.Save(); err == nil {
		t.Fatalf("expected Save to refuse while a required field is missing")
	}
	// The file on disk must be untouched.
	got, _ := os.ReadFile(path)
	if string(got) != "[model]\nMODEL=x\n" {
		t.Fatalf("file was modified despite a blocked save: %q", got)
	}
}

func TestSave_BacksUpAndWritesAtomically(t *testing.T) {
	dir := t.TempDir()
	path := filepath.Join(dir, "bravo.ini")
	original := "[model]\nMODEL=old\nBLOG=y\n"
	if err := os.WriteFile(path, []byte(original), 0o644); err != nil {
		t.Fatal(err)
	}
	doc, enc, err := ini.ReadFile(path, ini.DefaultParseOptions(), "")
	if err != nil {
		t.Fatal(err)
	}
	prof, _ := profile.Find("bravo")
	m := NewFormModel(prof, testSchema(), doc, enc, path)
	m.ApplyEdit("model", "MODEL", "new")

	backupPath, err := m.Save()
	if err != nil {
		t.Fatal(err)
	}
	if backupPath == "" {
		t.Fatalf("expected a non-empty backup path")
	}
	backupContent, err := os.ReadFile(backupPath)
	if err != nil {
		t.Fatal(err)
	}
	if string(backupContent) != original {
		t.Fatalf("backup content = %q, want original %q", backupContent, original)
	}

	saved, err := os.ReadFile(path)
	if err != nil {
		t.Fatal(err)
	}
	want := "[model]\nMODEL=new\nBLOG=y\n"
	if string(saved) != want {
		t.Fatalf("saved content = %q, want %q", saved, want)
	}
}

func TestSave_NewFile_NoBackupPath(t *testing.T) {
	dir := t.TempDir()
	path := filepath.Join(dir, "new-bravo.ini")
	doc, err := ini.Parse("", ini.DefaultParseOptions())
	if err != nil {
		t.Fatal(err)
	}
	doc.Set("model", "MODEL", "m")
	doc.Set("model", "BLOG", "b")

	prof, _ := profile.Find("bravo")
	m := NewFormModel(prof, testSchema(), doc, ini.EncodingUTF8, path)

	backupPath, err := m.Save()
	if err != nil {
		t.Fatal(err)
	}
	if backupPath != "" {
		t.Fatalf("expected no backup for a brand-new file, got %q", backupPath)
	}
	if _, err := os.Stat(path); err != nil {
		t.Fatalf("expected the new file to have been written: %v", err)
	}
}
