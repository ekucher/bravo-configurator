package app

import (
	"fmt"

	"github.com/ekucher/bravo-bis-configurator/internal/backup"
	"github.com/ekucher/bravo-bis-configurator/internal/ini"
)

// Save backs up m.FilePath (if it already exists) then atomically writes
// the current in-memory document to it, re-encoded with m.Encoding — the
// same encoding the file was originally read with, so a save never
// silently changes the codepage the external LIMS/BIS application expects.
//
// Save refuses to run while the model has any SeverityError finding,
// mirroring the GUI's disabled Save button, so the guard holds even if a
// caller reaches this method some other way than clicking Save.
//
// The returned backupPath is empty when m.FilePath did not exist yet
// (e.g. a brand-new file created from schema defaults) — there was
// nothing to back up.
func (m *FormModel) Save() (backupPath string, err error) {
	if !m.CanSave() {
		return "", fmt.Errorf("app: refusing to save while validation errors remain")
	}

	backupPath, err = backup.TimestampedBackup(m.FilePath)
	if err != nil {
		return "", fmt.Errorf("app: backup failed, original left untouched: %w", err)
	}

	data, err := ini.RenderFile(m.Doc, m.Encoding)
	if err != nil {
		return backupPath, fmt.Errorf("app: encode failed: %w", err)
	}
	if err := backup.AtomicWrite(m.FilePath, data); err != nil {
		return backupPath, fmt.Errorf("app: write failed: %w", err)
	}
	return backupPath, nil
}
