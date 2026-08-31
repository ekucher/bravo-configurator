// Package backup provides the two safety primitives every save goes
// through: a timestamped copy of the file being overwritten, and an
// atomic (temp-file + rename) write so a crash or failure mid-write never
// leaves a truncated/corrupt file in the target's place.
package backup

import (
	"fmt"
	"io"
	"os"
	"path/filepath"
	"time"
)

// timestampFormat produces names like bravo.ini.20260831-143012.bak. It
// deliberately excludes anything finer than seconds; RapidConsecutive
// collisions within the same second are resolved by uniquePath below
// rather than by adding sub-second precision, so backup filenames stay
// readable.
const timestampFormat = "20060102-150405"

// TimestampedBackup copies path to "<path>.<YYYYMMDD-HHMMSS>.bak" (with a
// "-N" suffix inserted before ".bak" if that name is already taken, e.g.
// from a backup made earlier in the same second) and returns the backup's
// path. If path does not exist yet (e.g. creating a brand-new file from
// schema defaults), there is nothing to back up: TimestampedBackup returns
// ("", nil) rather than an error.
//
// The backup is made by copying through a fresh read handle, not by
// renaming the original — so if the copy fails partway (disk full, I/O
// error), the original file at path is left completely untouched and the
// caller can abort the save before any mutation is attempted.
func TimestampedBackup(path string) (backupPath string, err error) {
	src, err := os.Open(path)
	if err != nil {
		if os.IsNotExist(err) {
			return "", nil
		}
		return "", fmt.Errorf("backup: open %s: %w", path, err)
	}
	defer src.Close()

	candidate := fmt.Sprintf("%s.%s.bak", path, time.Now().Format(timestampFormat))
	candidate = uniquePath(candidate)

	dst, err := os.OpenFile(candidate, os.O_WRONLY|os.O_CREATE|os.O_EXCL, 0o644)
	if err != nil {
		return "", fmt.Errorf("backup: create %s: %w", candidate, err)
	}
	// If anything below fails, remove the partial backup file rather than
	// leaving a corrupt .bak behind.
	ok := false
	defer func() {
		dst.Close()
		if !ok {
			os.Remove(candidate)
		}
	}()

	if _, err := io.Copy(dst, src); err != nil {
		return "", fmt.Errorf("backup: copy to %s: %w", candidate, err)
	}
	if err := dst.Sync(); err != nil {
		return "", fmt.Errorf("backup: sync %s: %w", candidate, err)
	}
	ok = true
	return candidate, nil
}

// uniquePath returns p unchanged if nothing exists at that path yet,
// otherwise inserts "-1", "-2", ... before the ".bak" extension until an
// unused name is found. This makes rapid consecutive saves (same second)
// produce distinct backups instead of one overwriting the other.
func uniquePath(p string) string {
	if _, err := os.Stat(p); os.IsNotExist(err) {
		return p
	}
	ext := filepath.Ext(p)
	base := p[:len(p)-len(ext)]
	for i := 1; ; i++ {
		candidate := fmt.Sprintf("%s-%d%s", base, i, ext)
		if _, err := os.Stat(candidate); os.IsNotExist(err) {
			return candidate
		}
	}
}
