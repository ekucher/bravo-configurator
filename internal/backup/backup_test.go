package backup

import (
	"bytes"
	"os"
	"path/filepath"
	"testing"
)

func TestTimestampedBackup_CreatesCopyWithExpectedNaming(t *testing.T) {
	dir := t.TempDir()
	path := filepath.Join(dir, "bravo.ini")
	content := []byte("[model]\nMODEL=x\n")
	if err := os.WriteFile(path, content, 0o644); err != nil {
		t.Fatal(err)
	}

	backupPath, err := TimestampedBackup(path)
	if err != nil {
		t.Fatal(err)
	}
	if backupPath == "" {
		t.Fatalf("expected a non-empty backup path")
	}
	if filepath.Dir(backupPath) != dir {
		t.Fatalf("backup created outside the source directory: %s", backupPath)
	}
	if !bytesEqualFile(t, backupPath, content) {
		t.Fatalf("backup content does not match source")
	}
	// Original must be untouched.
	if !bytesEqualFile(t, path, content) {
		t.Fatalf("original file was modified by TimestampedBackup")
	}
}

func TestTimestampedBackup_NonexistentSource_NoErrorNoBackup(t *testing.T) {
	dir := t.TempDir()
	path := filepath.Join(dir, "does-not-exist.ini")
	backupPath, err := TimestampedBackup(path)
	if err != nil {
		t.Fatalf("expected no error for a nonexistent source (nothing to back up), got %v", err)
	}
	if backupPath != "" {
		t.Fatalf("expected empty backup path, got %q", backupPath)
	}
}

func TestTimestampedBackup_RapidConsecutiveCallsGetUniqueNames(t *testing.T) {
	dir := t.TempDir()
	path := filepath.Join(dir, "bravo.ini")
	if err := os.WriteFile(path, []byte("v1"), 0o644); err != nil {
		t.Fatal(err)
	}

	first, err := TimestampedBackup(path)
	if err != nil {
		t.Fatal(err)
	}
	second, err := TimestampedBackup(path)
	if err != nil {
		t.Fatal(err)
	}
	if first == second {
		t.Fatalf("two consecutive backups produced the same path: %s", first)
	}
	if _, err := os.Stat(first); err != nil {
		t.Fatalf("first backup missing: %v", err)
	}
	if _, err := os.Stat(second); err != nil {
		t.Fatalf("second backup missing: %v", err)
	}
}

func bytesEqualFile(t *testing.T, path string, want []byte) bool {
	t.Helper()
	got, err := os.ReadFile(path)
	if err != nil {
		t.Fatal(err)
	}
	return bytes.Equal(got, want)
}
