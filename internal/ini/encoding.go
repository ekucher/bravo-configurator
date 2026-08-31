package ini

import (
	"bytes"
	"fmt"
	"unicode/utf8"

	"golang.org/x/text/encoding/charmap"
)

// Encoding identifies the byte-level text encoding a document was read
// with, so it can be written back with the same encoding by default
// instead of silently upgrading a legacy file's codepage underneath the
// external LIMS/BIS application that actually consumes it.
type Encoding string

const (
	// EncodingUTF8 is UTF-8 without a byte-order mark. This is the default
	// assumption: BRAVO-Toolkit's confirmed bravo.ini reader
	// (ConvertFrom-BRAVOIniFile) reads via `Get-Content -Encoding UTF8`,
	// so production files are UTF-8 in that deployment.
	EncodingUTF8 Encoding = "utf-8"
	// EncodingUTF8BOM is UTF-8 with a leading EF BB BF byte-order mark.
	EncodingUTF8BOM Encoding = "utf-8-bom"
	// EncodingCP1251 is the Windows-1251 (Cyrillic) codepage, offered as a
	// manual override for legacy files — not a verified production fact.
	EncodingCP1251 Encoding = "windows-1251"
	// EncodingCP1252 is the Windows-1252 (Western European) codepage,
	// offered as a manual override for legacy files.
	EncodingCP1252 Encoding = "windows-1252"
)

// DetectAndDecode inspects raw file bytes and returns the decoded UTF-8
// text plus the Encoding it used, so the caller can write the same
// encoding back on save. If forceEncoding is non-empty it is used as-is
// (manual override, e.g. from a GUI/CLI flag) instead of auto-detection.
//
// Auto-detection order: UTF-8 BOM -> plain UTF-8 (this also covers
// ASCII-only files, which are valid UTF-8) -> Windows-1251 as a defensive
// fallback for legacy non-UTF-8 files. The CP1251 fallback is a guess for
// old Windows deployments, not a verified fact — see docs/SCHEMA_STATUS.md.
func DetectAndDecode(raw []byte, forceEncoding Encoding) (text string, detected Encoding, err error) {
	if forceEncoding != "" {
		text, err = decodeAs(raw, forceEncoding)
		return text, forceEncoding, err
	}
	if bytes.HasPrefix(raw, []byte{0xEF, 0xBB, 0xBF}) {
		text, err = decodeAs(raw, EncodingUTF8BOM)
		return text, EncodingUTF8BOM, err
	}
	if utf8.Valid(raw) {
		return string(raw), EncodingUTF8, nil
	}
	text, err = decodeAs(raw, EncodingCP1251)
	return text, EncodingCP1251, err
}

func decodeAs(raw []byte, enc Encoding) (string, error) {
	switch enc {
	case EncodingUTF8:
		if !utf8.Valid(raw) {
			return "", fmt.Errorf("ini: content is not valid UTF-8")
		}
		return string(raw), nil
	case EncodingUTF8BOM:
		raw = bytes.TrimPrefix(raw, []byte{0xEF, 0xBB, 0xBF})
		if !utf8.Valid(raw) {
			return "", fmt.Errorf("ini: content is not valid UTF-8 (after BOM)")
		}
		return string(raw), nil
	case EncodingCP1251:
		decoded, err := charmap.Windows1251.NewDecoder().Bytes(raw)
		if err != nil {
			return "", fmt.Errorf("ini: decode windows-1251: %w", err)
		}
		return string(decoded), nil
	case EncodingCP1252:
		decoded, err := charmap.Windows1252.NewDecoder().Bytes(raw)
		if err != nil {
			return "", fmt.Errorf("ini: decode windows-1252: %w", err)
		}
		return string(decoded), nil
	default:
		return "", fmt.Errorf("ini: unsupported encoding %q", enc)
	}
}

// EncodeAs re-encodes text (decoded UTF-8 document text) back to raw bytes
// for enc.
func EncodeAs(text string, enc Encoding) ([]byte, error) {
	switch enc {
	case EncodingUTF8:
		return []byte(text), nil
	case EncodingUTF8BOM:
		return append([]byte{0xEF, 0xBB, 0xBF}, []byte(text)...), nil
	case EncodingCP1251:
		return charmap.Windows1251.NewEncoder().Bytes([]byte(text))
	case EncodingCP1252:
		return charmap.Windows1252.NewEncoder().Bytes([]byte(text))
	default:
		return nil, fmt.Errorf("ini: unsupported encoding %q", enc)
	}
}
