package profile

import "testing"

func TestAll_HasBothProfiles(t *testing.T) {
	all := All()
	if len(all) != 2 {
		t.Fatalf("expected 2 profiles, got %d", len(all))
	}
}

func TestFind_KnownAndUnknown(t *testing.T) {
	if _, ok := Find("bravo"); !ok {
		t.Fatalf("expected to find profile \"bravo\"")
	}
	if _, ok := Find("bis"); !ok {
		t.Fatalf("expected to find profile \"bis\"")
	}
	if _, ok := Find("nope"); ok {
		t.Fatalf("expected \"nope\" to be unknown")
	}
}

func TestLoadSchema_BothProfilesLoadTheirBundledSchema(t *testing.T) {
	for _, name := range []string{"bravo", "bis"} {
		p, ok := Find(name)
		if !ok {
			t.Fatalf("Find(%q) failed", name)
		}
		s, err := p.LoadSchema()
		if err != nil {
			t.Fatalf("LoadSchema(%q): %v", name, err)
		}
		if s.ProfileName != name {
			t.Fatalf("LoadSchema(%q).ProfileName = %q", name, s.ProfileName)
		}
	}
}
