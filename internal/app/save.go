package app

import (
	"fmt"
	"os"
	"path/filepath"
	"strings"

	"github.com/ekucher/bravo-bis-configurator/internal/backup"
	"github.com/ekucher/bravo-bis-configurator/internal/ini"
	"github.com/ekucher/bravo-bis-configurator/internal/locate"
)

// systemBravoIniPathFunc and executableDirFunc indirect internal/locate so
// tests can substitute deterministic values for rootCopyTarget without
// writing into the real system directory or the `go test` binary's own
// temp directory.
var (
	systemBravoIniPathFunc = func() (string, error) { return locate.SystemBravoIniPath(locate.Options{}) }
	executableDirFunc      = locate.ExecutableDir
)

// SaveResult reports what Save actually did, including the best-effort
// "root copy" side effect for the bravo profile: after saving the real,
// canonical bravo.ini in the OS system directory, this tool also mirrors
// it next to its own executable (where bis.ini lives), so an operator
// without access to browse the system directory can still see the
// current content. A failure there must never be reported as though the
// primary save (to m.FilePath) had failed, but it must also never be
// silently swallowed — see docs/ARCHITECTURE.md's "Auto-discovery"
// section.
type SaveResult struct {
	// BackupPath is empty when m.FilePath did not exist yet — there was
	// nothing to back up.
	BackupPath string
	// RootCopyPath is set when the active profile is "bravo", m.FilePath
	// is exactly the canonical system-directory bravo.ini (not some other
	// file an operator manually browsed to via the fallback dialog), and
	// the copy succeeded.
	RootCopyPath string
	// RootCopyErr is non-nil when a root copy was attempted (per
	// RootCopyPath's conditions) but failed. The primary save to
	// m.FilePath already succeeded in that case.
	RootCopyErr error
}

// Save backs up m.FilePath (if it already exists) then atomically writes
// the current in-memory document to it, re-encoded with m.Encoding — the
// same encoding the file was originally read with, so a save never
// silently changes the codepage the external LIMS/BIS application expects.
//
// Save refuses to run while the model has any SeverityError finding,
// mirroring the GUI's disabled Save button, so the guard holds even if a
// caller reaches this method some other way than clicking Save.
func (m *FormModel) Save() (SaveResult, error) {
	if !m.CanSave() {
		return SaveResult{}, fmt.Errorf("app: refusing to save while validation errors remain")
	}

	backupPath, err := backup.TimestampedBackup(m.FilePath)
	if err != nil {
		return SaveResult{}, fmt.Errorf("app: backup failed, original left untouched: %w", err)
	}

	data, err := ini.RenderFile(m.Doc, m.Encoding)
	if err != nil {
		return SaveResult{BackupPath: backupPath}, fmt.Errorf("app: encode failed: %w", err)
	}
	if err := backup.AtomicWrite(m.FilePath, data); err != nil {
		return SaveResult{BackupPath: backupPath}, fmt.Errorf("app: write failed: %w", err)
	}

	result := SaveResult{BackupPath: backupPath}
	if rootPath, ok := m.rootCopyTarget(); ok {
		if copyErr := os.WriteFile(rootPath, data, 0o644); copyErr != nil {
			result.RootCopyErr = fmt.Errorf("app: copying to %s: %w", rootPath, copyErr)
		} else {
			result.RootCopyPath = rootPath
		}
	}
	return result, nil
}

// rootCopyTarget reports whether m.FilePath is exactly the canonical
// system-directory bravo.ini and, if so, the path next to the running
// executable it should be mirrored to after a successful save.
func (m *FormModel) rootCopyTarget() (path string, ok bool) {
	if m.Profile.Name != "bravo" {
		return "", false
	}
	systemPath, err := systemBravoIniPathFunc()
	if err != nil || !samePath(systemPath, m.FilePath) {
		return "", false
	}
	dir, err := executableDirFunc()
	if err != nil {
		return "", false
	}
	return filepath.Join(dir, "bravo.ini"), true
}

// samePath compares two paths the way Windows does: case-insensitively,
// after normalizing separators/`.`/`..` segments.
func samePath(a, b string) bool {
	return strings.EqualFold(filepath.Clean(a), filepath.Clean(b))
}
