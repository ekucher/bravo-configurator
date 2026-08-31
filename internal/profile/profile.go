// Package profile is the small registry mapping a profile name ("bravo" or
// "bis") to its bundled schema and a suggested default filename for the
// GUI's file-open dialog / the CLI's --profile flag.
package profile

import "github.com/ekucher/bravo-bis-configurator/internal/schema"

// Profile identifies one of the two configuration files this tool edits.
type Profile struct {
	// Name is also the key schema.LoadEmbedded expects.
	Name        string
	DisplayName string
	// FileHint is the filename the GUI's file-open dialog defaults to.
	FileHint string
}

var registry = []Profile{
	{Name: "bravo", DisplayName: "BRAVO (сервер)", FileHint: "bravo.ini"},
	{Name: "bis", DisplayName: "BIS (клієнт)", FileHint: "bis.ini"},
}

// All returns every registered profile, in a stable, fixed order.
func All() []Profile {
	return append([]Profile(nil), registry...)
}

// Find looks up a profile by name.
func Find(name string) (Profile, bool) {
	for _, p := range registry {
		if p.Name == name {
			return p, true
		}
	}
	return Profile{}, false
}

// LoadSchema loads this profile's bundled default schema.
func (p Profile) LoadSchema() (*schema.Schema, error) {
	return schema.LoadEmbedded(p.Name)
}
