package app

import (
	"path/filepath"

	"github.com/ekucher/bravo-bis-configurator/internal/locate"
	"github.com/ekucher/bravo-bis-configurator/internal/profile"
)

// defaultPathForProfile returns prof's auto-discovered path:
//
//   - bravo.ini: the OS system directory (see internal/locate — bravo.exe,
//     the LIMS server component, is the canonical writer there; this
//     tool is not deployed next to the real bravo.ini).
//   - every other profile (bis.ini today): FileHint next to the running
//     configurator.exe, matching the LIMS client install layout this
//     tool ships inside alongside bis.ini.
func defaultPathForProfile(prof profile.Profile) (string, error) {
	if prof.Name == "bravo" {
		return locate.SystemBravoIniPath(locate.Options{})
	}
	dir, err := locate.ExecutableDir()
	if err != nil {
		return "", err
	}
	return filepath.Join(dir, prof.FileHint), nil
}
