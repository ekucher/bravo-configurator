package ini

import (
	"bytes"
	"os"
	"path/filepath"
	"testing"
	"unicode/utf8"

	"golang.org/x/text/encoding/charmap"
)

func TestDetectAndDecode_UTF8NoBOM(t *testing.T) {
	raw := []byte("[model]\nMODEL=C:\\LIMS\\MODEL\n")
	text, enc, err := DetectAndDecode(raw, "")
	if err != nil {
		t.Fatal(err)
	}
	if enc != EncodingUTF8 {
		t.Fatalf("detected %q, want %q", enc, EncodingUTF8)
	}
	if text != string(raw) {
		t.Fatalf("decoded text mismatch")
	}
}

func TestDetectAndDecode_UTF8BOM(t *testing.T) {
	raw := append([]byte{0xEF, 0xBB, 0xBF}, []byte("[model]\nMODEL=x\n")...)
	text, enc, err := DetectAndDecode(raw, "")
	if err != nil {
		t.Fatal(err)
	}
	if enc != EncodingUTF8BOM {
		t.Fatalf("detected %q, want %q", enc, EncodingUTF8BOM)
	}
	if text != "[model]\nMODEL=x\n" {
		t.Fatalf("BOM was not stripped from decoded text: %q", text)
	}
	// And it must round-trip back to byte-identical output including the BOM.
	out, err := EncodeAs(text, enc)
	if err != nil {
		t.Fatal(err)
	}
	if !bytes.Equal(out, raw) {
		t.Fatalf("EncodeAs(UTF8BOM) mismatch:\nwant %v\ngot  %v", raw, out)
	}
}

func TestDetectAndDecode_LegacyCP1251Fallback(t *testing.T) {
	// A value containing Cyrillic text, encoded as Windows-1251 (not valid
	// UTF-8), must be auto-detected and decoded correctly since it isn't
	// valid UTF-8.
	original := "[model]\nLABEL=Значення\n"
	cp1251Bytes, err := charmap.Windows1251.NewEncoder().Bytes([]byte(original))
	if err != nil {
		t.Fatal(err)
	}
	if utf8.Valid(cp1251Bytes) {
		t.Fatalf("test fixture bytes are unexpectedly valid UTF-8; can't exercise the fallback")
	}

	text, enc, err := DetectAndDecode(cp1251Bytes, "")
	if err != nil {
		t.Fatal(err)
	}
	if enc != EncodingCP1251 {
		t.Fatalf("detected %q, want %q", enc, EncodingCP1251)
	}
	if text != original {
		t.Fatalf("decoded text = %q, want %q", text, original)
	}

	// Round-trip back to the identical original bytes.
	out, err := EncodeAs(text, enc)
	if err != nil {
		t.Fatal(err)
	}
	if !bytes.Equal(out, cp1251Bytes) {
		t.Fatalf("EncodeAs(CP1251) did not reproduce original bytes")
	}
}

func TestDetectAndDecode_ForceEncodingOverride(t *testing.T) {
	raw, err := charmap.Windows1252.NewEncoder().Bytes([]byte("[model]\nLABEL=caf\u00e9\n"))
	if err != nil {
		t.Fatal(err)
	}
	text, enc, err := DetectAndDecode(raw, EncodingCP1252)
	if err != nil {
		t.Fatal(err)
	}
	if enc != EncodingCP1252 {
		t.Fatalf("forced encoding not honored: got %q", enc)
	}
	if text != "[model]\nLABEL=caf\u00e9\n" {
		t.Fatalf("decoded text mismatch: %q", text)
	}
}

func TestReadFile_RoundTripsSameEncoding(t *testing.T) {
	dir := t.TempDir()
	path := filepath.Join(dir, "bravo.ini")
	original := append([]byte{0xEF, 0xBB, 0xBF}, []byte("[model]\r\nMODEL=C:\\LIMS\\MODEL\r\n")...)
	if err := os.WriteFile(path, original, 0o644); err != nil {
		t.Fatal(err)
	}

	doc, enc, err := ReadFile(path, DefaultParseOptions(), "")
	if err != nil {
		t.Fatal(err)
	}
	if enc != EncodingUTF8BOM {
		t.Fatalf("detected %q, want %q", enc, EncodingUTF8BOM)
	}

	out, err := RenderFile(doc, enc)
	if err != nil {
		t.Fatal(err)
	}
	if !bytes.Equal(out, original) {
		t.Fatalf("ReadFile+RenderFile did not reproduce the original bytes:\nwant %v\ngot  %v", original, out)
	}
}
