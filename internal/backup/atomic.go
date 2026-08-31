package backup

import (
	"fmt"
	"os"
	"path/filepath"
)

// AtomicWrite writes data to path without ever leaving a partially-written
// file in its place: it writes to a temp file in the same directory
// (guaranteeing same-volume rename, which is atomic on NTFS/most
// filesystems), syncs it, closes it, then renames it over path. If any
// step before the rename fails, the temp file is removed and path is left
// completely untouched. Callers that want a safety copy of the previous
// contents should call TimestampedBackup(path) first.
func AtomicWrite(path string, data []byte) (err error) {
	dir := filepath.Dir(path)
	tmp, err := os.CreateTemp(dir, ".tmp-"+filepath.Base(path)+"-*")
	if err != nil {
		return fmt.Errorf("backup: create temp file in %s: %w", dir, err)
	}
	tmpPath := tmp.Name()

	renamed := false
	defer func() {
		if !renamed {
			tmp.Close()
			os.Remove(tmpPath)
		}
	}()

	if _, err := tmp.Write(data); err != nil {
		return fmt.Errorf("backup: write temp file %s: %w", tmpPath, err)
	}
	if err := tmp.Sync(); err != nil {
		return fmt.Errorf("backup: sync temp file %s: %w", tmpPath, err)
	}
	if err := tmp.Close(); err != nil {
		return fmt.Errorf("backup: close temp file %s: %w", tmpPath, err)
	}
	if err := os.Rename(tmpPath, path); err != nil {
		return fmt.Errorf("backup: rename %s to %s: %w", tmpPath, path, err)
	}
	renamed = true
	return nil
}
