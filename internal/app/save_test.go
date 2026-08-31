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
	// Note: this profile is "bravo", but m.FilePath here is a t.TempDir()
	// path that never matches the real system bravo.ini, so no
	// root-copy side effect is possible even if the guard above were
	// somehow bypassed — see TestSave_RootCopy_* below for that behavior.
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

	result, err := m.Save()
	if err != nil {
		t.Fatal(err)
	}
	if result.BackupPath == "" {
		t.Fatalf("expected a non-empty backup path")
	}
	if result.RootCopyPath != "" || result.RootCopyErr != nil {
		t.Fatalf("expected no root-copy attempt for a non-canonical path, got RootCopyPath=%q RootCopyErr=%v", result.RootCopyPath, result.RootCopyErr)
	}
	backupContent, err := os.ReadFile(result.BackupPath)
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

	result, err := m.Save()
	if err != nil {
		t.Fatal(err)
	}
	if result.BackupPath != "" {
		t.Fatalf("expected no backup for a brand-new file, got %q", result.BackupPath)
	}
	if _, err := os.Stat(path); err != nil {
		t.Fatalf("expected the new file to have been written: %v", err)
	}
}

// TestSave_RootCopy_MirrorsToExecutableDirWhenPathIsCanonical exercises
// Save's "copy bravo.ini next to the executable" side effect by
// substituting systemBravoIniPathFunc/executableDirFunc for the duration
// of the test, so it never touches the real system directory or the `go
// test` binary's own temp directory.
func TestSave_RootCopy_MirrorsToExecutableDirWhenPathIsCanonical(t *testing.T) {
	systemDir := t.TempDir()
	rootDir := t.TempDir()
	canonicalPath := filepath.Join(systemDir, "bravo.ini")

	original := "[model]\nMODEL=old\nBLOG=y\n"
	if err := os.WriteFile(canonicalPath, []byte(original), 0o644); err != nil {
		t.Fatal(err)
	}

	restore := stubLocateFuncs(t, canonicalPath, rootDir)
	defer restore()

	doc, enc, err := ini.ReadFile(canonicalPath, ini.DefaultParseOptions(), "")
	if err != nil {
		t.Fatal(err)
	}
	prof, _ := profile.Find("bravo")
	m := NewFormModel(prof, testSchema(), doc, enc, canonicalPath)
	m.ApplyEdit("model", "MODEL", "new")

	result, err := m.Save()
	if err != nil {
		t.Fatal(err)
	}
	if result.RootCopyErr != nil {
		t.Fatalf("unexpected RootCopyErr: %v", result.RootCopyErr)
	}
	wantRootPath := filepath.Join(rootDir, "bravo.ini")
	if result.RootCopyPath != wantRootPath {
		t.Fatalf("RootCopyPath = %q, want %q", result.RootCopyPath, wantRootPath)
	}
	rootContent, err := os.ReadFile(wantRootPath)
	if err != nil {
		t.Fatal(err)
	}
	wantContent := "[model]\nMODEL=new\nBLOG=y\n"
	if string(rootContent) != wantContent {
		t.Fatalf("root copy content = %q, want %q", rootContent, wantContent)
	}
}

// TestSave_RootCopy_SkippedForBisProfile confirms the root-copy side
// effect is bravo-only: bis.ini is already read from (and saved to) the
// executable's own directory, so mirroring it a second time makes no
// sense.
func TestSave_RootCopy_SkippedForBisProfile(t *testing.T) {
	dir := t.TempDir()
	path := filepath.Join(dir, "bis.ini")
	if err := os.WriteFile(path, []byte("[model]\nMODEL=x\nBLOG=y\n"), 0o644); err != nil {
		t.Fatal(err)
	}
	doc, enc, err := ini.ReadFile(path, ini.DefaultParseOptions(), "")
	if err != nil {
		t.Fatal(err)
	}
	prof, _ := profile.Find("bis")
	m := NewFormModel(prof, testSchema(), doc, enc, path)

	result, err := m.Save()
	if err != nil {
		t.Fatal(err)
	}
	if result.RootCopyPath != "" || result.RootCopyErr != nil {
		t.Fatalf("expected no root-copy attempt for the bis profile, got RootCopyPath=%q RootCopyErr=%v", result.RootCopyPath, result.RootCopyErr)
	}
}

// stubLocateFuncs substitutes systemBravoIniPathFunc/executableDirFunc so
// rootCopyTarget resolves deterministically to test-controlled
// directories, and restores the originals via t.Cleanup.
func stubLocateFuncs(t *testing.T, canonicalBravoIniPath, execDir string) func() {
	t.Helper()
	origSystemPath, origExecDir := systemBravoIniPathFunc, executableDirFunc
	systemBravoIniPathFunc = func() (string, error) { return canonicalBravoIniPath, nil }
	executableDirFunc = func() (string, error) { return execDir, nil }
	restore := func() {
		systemBravoIniPathFunc = origSystemPath
		executableDirFunc = origExecDir
	}
	t.Cleanup(restore)
	return restore
}
