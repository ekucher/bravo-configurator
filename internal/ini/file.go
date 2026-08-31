package ini

import "os"

// ReadFile reads path, auto-detects (or applies forceEncoding, if
// non-empty) its text encoding, and parses it with opts.
func ReadFile(path string, opts ParseOptions, forceEncoding Encoding) (doc *Document, enc Encoding, err error) {
	raw, err := os.ReadFile(path)
	if err != nil {
		return nil, "", err
	}
	text, enc, err := DetectAndDecode(raw, forceEncoding)
	if err != nil {
		return nil, "", err
	}
	doc, err = Parse(text, opts)
	if err != nil {
		return nil, "", err
	}
	return doc, enc, nil
}

// RenderFile serializes doc and re-encodes it as enc, returning the raw
// bytes ready to be written to disk. It does not write the file itself —
// callers should route the result through internal/backup.AtomicWrite so
// saves are backed up and atomic.
func RenderFile(doc *Document, enc Encoding) ([]byte, error) {
	text := Write(doc)
	return EncodeAs(text, enc)
}
