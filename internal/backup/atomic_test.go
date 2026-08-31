package backup

import (
	"bytes"
	"os"
	"path/filepath"
	"testing"
)

func TestAtomicWrite_CreatesNewFile(t *testing.T) {
	dir := t.TempDir()
	path := filepath.Join(dir, "bravo.ini")
	content := []byte("[model]\nMODEL=x\n")

	if err := AtomicWrite(path, content); err != nil {
		t.Fatal(err)
	}
	if !bytesEqualFile(t, path, content) {
		t.Fatalf("written content mismatch")
	}
	// No stray temp files left behind.
	assertNoTempFilesLeftBehind(t, dir)
}

func TestAtomicWrite_ReplacesExistingFile(t *testing.T) {
	dir := t.TempDir()
	path := filepath.Join(dir, "bravo.ini")
	if err := os.WriteFile(path, []byte("old"), 0o644); err != nil {
		t.Fatal(err)
	}
	newContent := []byte("[model]\nMODEL=new\n")
	if err := AtomicWrite(path, newContent); err != nil {
		t.Fatal(err)
	}
	if !bytesEqualFile(t, path, newContent) {
		t.Fatalf("replaced content mismatch")
	}
	assertNoTempFilesLeftBehind(t, dir)
}

func TestAtomicWrite_FailedRenameLeavesOriginalUntouched(t *testing.T) {
	dir := t.TempDir()
	original := []byte("original-content")
	// Use a directory as the destination "path" so the final os.Rename
	// step is guaranteed to fail (can't rename a regular temp file onto an
	// existing directory), letting us verify the pre-rename content is
	// left exactly as it was and no partial file appears in its place.
	targetDir := filepath.Join(dir, "bravo.ini") // name reused as a directory below
	if err := os.Mkdir(targetDir, 0o755); err != nil {
		t.Fatal(err)
	}
	marker := filepath.Join(targetDir, "marker.txt")
	if err := os.WriteFile(marker, original, 0o644); err != nil {
		t.Fatal(err)
	}

	err := AtomicWrite(targetDir, []byte("new-content"))
	if err == nil {
		t.Fatalf("expected AtomicWrite to fail when the destination is a directory")
	}
	// The directory (and the marker file proving it wasn't replaced) must
	// still be exactly as before.
	if !bytesEqualFile(t, marker, original) {
		t.Fatalf("destination directory was disturbed despite the failed rename")
	}
	assertNoTempFilesLeftBehind(t, dir)
}

func assertNoTempFilesLeftBehind(t *testing.T, dir string) {
	t.Helper()
	entries, err := os.ReadDir(dir)
	if err != nil {
		t.Fatal(err)
	}
	for _, e := range entries {
		if bytes.HasPrefix([]byte(e.Name()), []byte(".tmp-")) {
			t.Fatalf("stray temp file left behind: %s", e.Name())
		}
	}
}
