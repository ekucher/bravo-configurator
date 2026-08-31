// Package locate resolves the two well-known on-disk locations this tool
// auto-discovers its configuration files from, instead of requiring an
// operator to browse for them every time:
//
//   - bravo.ini lives in the OS system directory (bravo.exe is the
//     canonical writer; see SystemBravoIniPath).
//   - bis.ini lives next to the running configurator.exe itself, in
//     whatever directory the LIMS client install placed it.
//
// See docs/ARCHITECTURE.md's "Auto-discovery" section for the deployment
// rationale.
package locate

import (
	"errors"
	"fmt"
	"os"
	"path/filepath"
	"runtime"

	"golang.org/x/sys/windows"
)

// Options overrides real-environment inputs for deterministic tests.
// The zero value uses the real environment (os.Getenv("SystemRoot"),
// actual OS-bitness detection) — mirrors BRAVO-Toolkit's
// Get-BRAVOSystemDirectoryPath (modules/BRAVO.Discovery/BRAVO.Discovery.psm1),
// which takes the same two overridable inputs for the same reason.
type Options struct {
	// SystemRoot overrides os.Getenv("SystemRoot") when non-empty.
	SystemRoot string
	// Is64BitOS overrides the real OS-bitness detection when non-nil.
	Is64BitOS *bool
}

// SystemDirectory returns the OS system directory that owns bravo.ini:
// SysWOW64 on a 64-bit OS, System32 on a 32-bit OS.
//
// bravo.exe (the LIMS server component that owns bravo.ini) is a 32-bit
// process. On a 64-bit OS, a 32-bit process's accesses to "System32" are
// transparently redirected by WOW64 to SysWOW64 — SysWOW64 is therefore
// the one real, absolute directory bravo.ini lives in on disk, regardless
// of which process (32- or 64-bit) later reads that literal path. This
// exactly mirrors BRAVO-Toolkit's Get-BRAVOSystemDirectoryPath so both
// tools agree on the same authoritative location.
func SystemDirectory(opts Options) (string, error) {
	systemRoot := opts.SystemRoot
	if systemRoot == "" {
		systemRoot = os.Getenv("SystemRoot")
	}
	if systemRoot == "" {
		return "", errors.New("locate: %SystemRoot% is not set and no override was given")
	}

	is64 := opts.Is64BitOS
	if is64 == nil {
		detected, err := is64BitOS()
		if err != nil {
			return "", fmt.Errorf("locate: determining OS bitness: %w", err)
		}
		is64 = &detected
	}

	sub := "System32"
	if *is64 {
		sub = "SysWOW64"
	}
	return filepath.Join(systemRoot, sub), nil
}

// SystemBravoIniPath returns the canonical, authoritative path to the
// server-side bravo.ini: SystemDirectory()\bravo.ini.
func SystemBravoIniPath(opts Options) (string, error) {
	dir, err := SystemDirectory(opts)
	if err != nil {
		return "", err
	}
	return filepath.Join(dir, "bravo.ini"), nil
}

// ExecutableDir returns the directory containing the running executable.
// Any symlink is resolved (os.Executable's own documentation recommends
// this for a canonical path); if resolution fails, the unresolved path is
// used rather than failing the whole lookup over a symlink quirk.
func ExecutableDir() (string, error) {
	exe, err := os.Executable()
	if err != nil {
		return "", fmt.Errorf("locate: os.Executable: %w", err)
	}
	resolved, err := filepath.EvalSymlinks(exe)
	if err != nil {
		resolved = exe
	}
	return filepath.Dir(resolved), nil
}

// is64BitOS reports whether the operating system (not just this process)
// is 64-bit, matching .NET's [Environment]::Is64BitOperatingSystem
// semantics that BRAVO-Toolkit's PowerShell equivalent relies on.
func is64BitOS() (bool, error) {
	if runtime.GOARCH == "amd64" || runtime.GOARCH == "arm64" {
		// A native 64-bit process can only run on a 64-bit OS.
		return true, nil
	}
	// A 32-bit build can run on either a 32-bit OS or, under WOW64, a
	// 64-bit one — ask Windows which.
	var wow64 bool
	if err := windows.IsWow64Process(windows.CurrentProcess(), &wow64); err != nil {
		return false, err
	}
	return wow64, nil
}
