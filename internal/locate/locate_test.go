package locate

import (
	"os"
	"path/filepath"
	"testing"
)

func boolPtr(b bool) *bool { return &b }

func TestSystemDirectory_64BitOS_UsesSysWOW64(t *testing.T) {
	got, err := SystemDirectory(Options{SystemRoot: `C:\Windows`, Is64BitOS: boolPtr(true)})
	if err != nil {
		t.Fatal(err)
	}
	want := filepath.Join(`C:\Windows`, "SysWOW64")
	if got != want {
		t.Fatalf("got %q, want %q", got, want)
	}
}

func TestSystemDirectory_32BitOS_UsesSystem32(t *testing.T) {
	got, err := SystemDirectory(Options{SystemRoot: `C:\Windows`, Is64BitOS: boolPtr(false)})
	if err != nil {
		t.Fatal(err)
	}
	want := filepath.Join(`C:\Windows`, "System32")
	if got != want {
		t.Fatalf("got %q, want %q", got, want)
	}
}

func TestSystemDirectory_MissingSystemRoot_Errors(t *testing.T) {
	t.Setenv("SystemRoot", "")
	if _, err := SystemDirectory(Options{Is64BitOS: boolPtr(true)}); err == nil {
		t.Fatal("expected an error when %SystemRoot% is unset and not overridden")
	}
}

func TestSystemDirectory_RealEnvironment_NoOverride(t *testing.T) {
	// Sanity check against the real environment this test runs in
	// (a real Windows machine per BUILDING.md's Windows-only policy):
	// just confirm it resolves without error and picks one of the two
	// known subdirectory names.
	got, err := SystemDirectory(Options{})
	if err != nil {
		t.Fatal(err)
	}
	base := filepath.Base(got)
	if base != "SysWOW64" && base != "System32" {
		t.Fatalf("unexpected system subdirectory: %q", got)
	}
}

func TestSystemBravoIniPath_JoinsFileName(t *testing.T) {
	got, err := SystemBravoIniPath(Options{SystemRoot: `C:\Windows`, Is64BitOS: boolPtr(true)})
	if err != nil {
		t.Fatal(err)
	}
	want := filepath.Join(`C:\Windows`, "SysWOW64", "bravo.ini")
	if got != want {
		t.Fatalf("got %q, want %q", got, want)
	}
}

func TestExecutableDir_ResolvesToAnExistingDirectory(t *testing.T) {
	dir, err := ExecutableDir()
	if err != nil {
		t.Fatal(err)
	}
	info, err := os.Stat(dir)
	if err != nil {
		t.Fatalf("ExecutableDir() = %q does not exist: %v", dir, err)
	}
	if !info.IsDir() {
		t.Fatalf("ExecutableDir() = %q is not a directory", dir)
	}
}
