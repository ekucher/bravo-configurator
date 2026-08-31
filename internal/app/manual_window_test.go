package app

import (
	"os"
	"testing"

	"github.com/ekucher/bravo-bis-configurator/internal/ini"
	"github.com/ekucher/bravo-bis-configurator/internal/profile"
)

// TestManualEditorWindow opens the real editor window directly against the
// bundled bravo schema, bypassing the profile/file/encoding dialogs. It is
// a manual diagnostic, not part of the normal test suite: MainWindow.Run()
// blocks until an operator closes the window, so this is skipped unless
// RUN_MANUAL_GUI_TEST=1 is set (`go test ./...` must never hang waiting
// for a human on a build machine or CI runner). It proved essential during
// development to isolate two real bugs that only reproduce once a
// MainWindow with many fields is actually constructed (see
// docs/ARCHITECTURE.md's "Bugs found via manual GUI testing" section) —
// kept as a fast way to reproduce them again if window.go changes.
//
// Run explicitly:
//
//	RUN_MANUAL_GUI_TEST=1 go test -run TestManualEditorWindow -v ./internal/app/
func TestManualEditorWindow(t *testing.T) {
	if os.Getenv("RUN_MANUAL_GUI_TEST") != "1" {
		t.Skip("manual diagnostic only; set RUN_MANUAL_GUI_TEST=1 to run (opens a real window and blocks until closed)")
	}
	doc, err := ini.Parse("[model]\nMODEL=C:\\Windows\nBLOG=C:\\Windows\nBEXCH=C:\\Windows\n", ini.DefaultParseOptions())
	if err != nil {
		t.Fatal(err)
	}
	prof, _ := profile.Find("bravo")
	s, err := prof.LoadSchema()
	if err != nil {
		t.Fatal(err)
	}
	model := NewFormModel(prof, s, doc, ini.EncodingUTF8, "test.ini")
	t.Logf("model built, sections=%d", len(model.Sections))
	err = runEditorWindow(model)
	t.Logf("runEditorWindow returned err=%v", err)
}
