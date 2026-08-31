package ini

import (
	"strings"
	"testing"
)

// roundTrip is the single most important property of this package: parsing
// a file and writing it back out without touching anything must reproduce
// the source byte-for-byte.
func roundTrip(t *testing.T, content string, opts ParseOptions) *Document {
	t.Helper()
	doc, err := Parse(content, opts)
	if err != nil {
		t.Fatalf("Parse: %v", err)
	}
	got := Write(doc)
	if got != content {
		t.Fatalf("round-trip mismatch:\n--- want ---\n%q\n--- got ---\n%q", content, got)
	}
	return doc
}

func TestRoundTrip_BravoShapedSample_LF(t *testing.T) {
	content := "; bravo.ini sample\n" +
		"[system]\n" +
		"CustomKey=value\n" +
		"\n" +
		"[model]\n" +
		"MODEL=C:\\LIMS\\MODEL\n" +
		"BLOG=C:\\LIMS\\BLOG\n" +
		"BEXCH=C:\\LIMS\\BRAVOEXCH\\\n" +
		"\n" +
		"[Debug]\n" +
		"FILE=Trace\\TraceSRV.out ; relative to install dir\n"
	doc := roundTrip(t, content, DefaultParseOptions())

	if v, ok := doc.Get("model", "MODEL"); !ok || v != `C:\LIMS\MODEL` {
		t.Fatalf("Get(model,MODEL) = %q, %v", v, ok)
	}
	// Case-insensitive section/key lookup, per confirmed production behavior.
	if v, ok := doc.Get("MODEL", "model"); !ok || v != `C:\LIMS\MODEL` {
		t.Fatalf("case-insensitive Get(MODEL,model) = %q, %v", v, ok)
	}
	if v, ok := doc.Get("Debug", "FILE"); !ok || v != `Trace\TraceSRV.out` {
		t.Fatalf("Get(Debug,FILE) = %q, %v (inline comment should be split off)", v, ok)
	}
}

func TestRoundTrip_CRLF(t *testing.T) {
	content := "[model]\r\nMODEL=C:\\LIMS\\MODEL\r\nBLOG=C:\\LIMS\\BLOG\r\n"
	roundTrip(t, content, DefaultParseOptions())
}

func TestRoundTrip_NoTrailingNewline(t *testing.T) {
	content := "[model]\nMODEL=C:\\LIMS\\MODEL"
	doc := roundTrip(t, content, DefaultParseOptions())
	if doc.TrailingNewline {
		t.Fatalf("expected TrailingNewline=false")
	}
}

func TestRoundTrip_EmptyFile(t *testing.T) {
	roundTrip(t, "", DefaultParseOptions())
}

func TestRoundTrip_OnlyBlankLinesAndComments(t *testing.T) {
	content := "\n; leading comment\n\n; another\n"
	roundTrip(t, content, DefaultParseOptions())
}

func TestRoundTrip_MalformedLinesPassThroughVerbatim(t *testing.T) {
	content := "[model]\nMODEL=C:\\LIMS\\MODEL\nthis line has no separator\n=novalue key is empty too\n"
	doc := roundTrip(t, content, DefaultParseOptions())

	sec, ok := doc.Sections[1], true
	_ = ok
	var rawCount int
	for _, e := range sec.Entries {
		if e.Kind == KindRaw {
			rawCount++
		}
	}
	if rawCount != 2 {
		t.Fatalf("expected 2 KindRaw entries (no separator, empty key), got %d", rawCount)
	}
}

func TestDuplicateKeys_LastWins(t *testing.T) {
	content := "[model]\nMODEL=first\nMODEL=second\n"
	doc := roundTrip(t, content, DefaultParseOptions()) // LastWins is the default
	if v, _ := doc.Get("model", "MODEL"); v != "second" {
		t.Fatalf("LastWins: got %q, want %q", v, "second")
	}
}

func TestDuplicateKeys_FirstWins(t *testing.T) {
	content := "[model]\nMODEL=first\nMODEL=second\n"
	opts := DefaultParseOptions()
	opts.DuplicateKeyPolicy = FirstWins
	doc := roundTrip(t, content, opts)
	if v, _ := doc.Get("model", "MODEL"); v != "first" {
		t.Fatalf("FirstWins: got %q, want %q", v, "first")
	}
}

func TestDuplicateKeys_ErrorOnDuplicate(t *testing.T) {
	content := "[model]\nMODEL=first\nMODEL=second\n"
	opts := DefaultParseOptions()
	opts.DuplicateKeyPolicy = ErrorOnDuplicate
	_, err := Parse(content, opts)
	var dupErr *DuplicateKeyError
	if err == nil {
		t.Fatalf("expected DuplicateKeyError, got nil")
	}
	if !asDuplicateKeyError(err, &dupErr) {
		t.Fatalf("expected *DuplicateKeyError, got %T: %v", err, err)
	}
	if dupErr.Key != "MODEL" || dupErr.Section != "model" || dupErr.Line != 3 {
		t.Fatalf("unexpected DuplicateKeyError: %+v", dupErr)
	}
}

func TestDuplicateKeys_ErrorOnDuplicate_AcrossRepeatedSectionBlocks(t *testing.T) {
	// Same section name reopened later in the file must still be treated as
	// one logical section for duplicate detection, matching the confirmed
	// reader's single-hashtable-per-name merge semantics.
	content := "[model]\nMODEL=first\n[other]\nX=1\n[model]\nMODEL=second\n"
	opts := DefaultParseOptions()
	opts.DuplicateKeyPolicy = ErrorOnDuplicate
	_, err := Parse(content, opts)
	if err == nil {
		t.Fatalf("expected DuplicateKeyError across repeated [model] blocks, got nil")
	}
}

func asDuplicateKeyError(err error, target **DuplicateKeyError) bool {
	if e, ok := err.(*DuplicateKeyError); ok {
		*target = e
		return true
	}
	return false
}

func TestGet_UnknownSectionOrKey(t *testing.T) {
	doc, err := Parse("[model]\nMODEL=x\n", DefaultParseOptions())
	if err != nil {
		t.Fatal(err)
	}
	if _, ok := doc.Get("missing", "MODEL"); ok {
		t.Fatalf("expected ok=false for missing section")
	}
	if _, ok := doc.Get("model", "missing"); ok {
		t.Fatalf("expected ok=false for missing key")
	}
}

func TestSet_UpdatesInPlace_PreservesRestOfFile(t *testing.T) {
	content := "; header comment\n[model]\nMODEL=C:\\old\nBLOG=C:\\LIMS\\BLOG ; keep this comment\n"
	doc, err := Parse(content, DefaultParseOptions())
	if err != nil {
		t.Fatal(err)
	}
	doc.Set("model", "MODEL", `C:\new`)
	got := Write(doc)

	want := "; header comment\n[model]\nMODEL=C:\\new\nBLOG=C:\\LIMS\\BLOG ; keep this comment\n"
	if got != want {
		t.Fatalf("Set round-trip mismatch:\n--- want ---\n%q\n--- got ---\n%q", want, got)
	}
}

func TestSet_UnknownKeyRoundTripsUnchangedWhenNotEdited(t *testing.T) {
	// A field the schema doesn't know about must survive edit+save of a
	// different field, untouched.
	content := "[model]\nMODEL=C:\\LIMS\\MODEL\nSomeFutureKey=keep-me-exactly\n"
	doc, err := Parse(content, DefaultParseOptions())
	if err != nil {
		t.Fatal(err)
	}
	doc.Set("model", "MODEL", `C:\LIMS\MODEL2`)
	got := Write(doc)
	if !strings.Contains(got, "SomeFutureKey=keep-me-exactly\n") {
		t.Fatalf("unknown key was not preserved verbatim:\n%s", got)
	}
}

func TestSet_AppendsNewKeyToExistingSection(t *testing.T) {
	doc, err := Parse("[model]\nMODEL=C:\\LIMS\\MODEL\n", DefaultParseOptions())
	if err != nil {
		t.Fatal(err)
	}
	doc.Set("model", "BLOG", `C:\LIMS\BLOG`)
	got := Write(doc)
	want := "[model]\nMODEL=C:\\LIMS\\MODEL\nBLOG=C:\\LIMS\\BLOG\n"
	if got != want {
		t.Fatalf("append mismatch:\n--- want ---\n%q\n--- got ---\n%q", want, got)
	}
}

func TestSet_CreatesNewSectionWhenAbsent(t *testing.T) {
	doc, err := Parse("[model]\nMODEL=x\n", DefaultParseOptions())
	if err != nil {
		t.Fatal(err)
	}
	doc.Set("Debug", "FILE", `Trace\TraceSRV.out`)
	got := Write(doc)
	want := "[model]\nMODEL=x\n[Debug]\nFILE=Trace\\TraceSRV.out\n"
	if got != want {
		t.Fatalf("new-section mismatch:\n--- want ---\n%q\n--- got ---\n%q", want, got)
	}
}
