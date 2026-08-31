package main

import (
	"bytes"
	"os"
	"path/filepath"
	"strings"
	"testing"
)

func TestRun_ValidateKnownGoodFile_ExitsZero(t *testing.T) {
	dir := t.TempDir()
	path := filepath.Join(dir, "bravo.ini")
	// Only the required, verified fields; everything else optional.
	content := "[model]\nMODEL=" + dir + "\nBLOG=" + dir + "\nBEXCH=" + dir + "\n"
	if err := os.WriteFile(path, []byte(content), 0o644); err != nil {
		t.Fatal(err)
	}

	var stdout, stderr bytes.Buffer
	code := run([]string{"--validate", "--profile", "bravo", "--file", path}, &stdout, &stderr)
	if code != 0 {
		t.Fatalf("exit code = %d, want 0; stdout=%s stderr=%s", code, stdout.String(), stderr.String())
	}
}

func TestRun_ValidateMissingRequiredField_ExitsNonZero(t *testing.T) {
	dir := t.TempDir()
	path := filepath.Join(dir, "bravo.ini")
	if err := os.WriteFile(path, []byte("[model]\nMODEL="+dir+"\n"), 0o644); err != nil {
		t.Fatal(err)
	}

	var stdout, stderr bytes.Buffer
	code := run([]string{"--validate", "--profile", "bravo", "--file", path}, &stdout, &stderr)
	if code != 1 {
		t.Fatalf("exit code = %d, want 1 (missing required BLOG/BEXCH); stdout=%s", code, stdout.String())
	}
	if !strings.Contains(stdout.String(), "BLOG") {
		t.Fatalf("expected the missing-field finding to name BLOG, got: %s", stdout.String())
	}
}

func TestRun_UnknownProfile_ExitsUsageError(t *testing.T) {
	var stdout, stderr bytes.Buffer
	code := run([]string{"--validate", "--profile", "nope", "--file", "x"}, &stdout, &stderr)
	if code != 2 {
		t.Fatalf("exit code = %d, want 2", code)
	}
}

// Note: there is no automated test for run(nil, ...) (the no-flags path),
// because that path calls app.RunGUI(), which creates a real Win32 window
// and blocks on a message loop — it requires an interactive Windows
// desktop session and cannot run headlessly in `go test`. See the manual
// GUI checklist in docs/BUILDING.md instead.

func TestRun_ValidateWithCustomSchemaOverride(t *testing.T) {
	dir := t.TempDir()
	iniPath := filepath.Join(dir, "custom.ini")
	if err := os.WriteFile(iniPath, []byte("[x]\nY=hello\n"), 0o644); err != nil {
		t.Fatal(err)
	}
	schemaPath := filepath.Join(dir, "custom.schema.yaml")
	schemaDoc := `
profile: bravo
status: verified
sections:
  - name: x
    fields:
      - key: Y
        type: string
        required: true
`
	if err := os.WriteFile(schemaPath, []byte(schemaDoc), 0o644); err != nil {
		t.Fatal(err)
	}

	var stdout, stderr bytes.Buffer
	code := run([]string{"--validate", "--profile", "bravo", "--file", iniPath, "--schema", schemaPath}, &stdout, &stderr)
	if code != 0 {
		t.Fatalf("exit code = %d, want 0; stdout=%s stderr=%s", code, stdout.String(), stderr.String())
	}
}

func TestRun_ValidateMissingFileArgs_UsageError(t *testing.T) {
	var stdout, stderr bytes.Buffer
	code := run([]string{"--validate", "--profile", "bravo"}, &stdout, &stderr)
	if code != 2 {
		t.Fatalf("exit code = %d, want 2", code)
	}
}

func TestRun_ValidateNonexistentFile_UsageError(t *testing.T) {
	var stdout, stderr bytes.Buffer
	code := run([]string{"--validate", "--profile", "bravo", "--file", "does-not-exist.ini"}, &stdout, &stderr)
	if code != 2 {
		t.Fatalf("exit code = %d, want 2", code)
	}
}
