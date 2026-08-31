package ini

import (
	"fmt"
	"strings"
	"unicode"
)

// DuplicateKeyError is returned by Parse when opts.DuplicateKeyPolicy is
// ErrorOnDuplicate and a key repeats within the same logical section
// (including across two separate "[Section]" blocks sharing a name).
type DuplicateKeyError struct {
	Section string
	Key     string
	Line    int
}

func (e *DuplicateKeyError) Error() string {
	return fmt.Sprintf("ini: duplicate key %q in section %q at line %d", e.Key, e.Section, e.Line)
}

// Parse builds a Document from already-decoded (UTF-8) INI text. Use
// ReadFile to parse directly from disk with automatic encoding detection.
//
// Every line that isn't turned into a KindKeyValue/blank/comment is kept as
// a verbatim KindRaw entry, and every parsed line keeps its OriginalLine,
// so Write reproduces the source exactly for anything the caller does not
// edit via Document.Set.
func Parse(content string, opts ParseOptions) (*Document, error) {
	if len(opts.CommentPrefixes) == 0 {
		opts = DefaultParseOptions()
	}

	doc := &Document{caseInsensitive: opts.CaseInsensitiveKeys, dupPolicy: opts.DuplicateKeyPolicy}

	if strings.Contains(content, "\r\n") {
		doc.LineEnding = "\r\n"
	} else {
		doc.LineEnding = "\n"
	}
	normalized := strings.ReplaceAll(content, "\r\n", "\n")

	var lines []string
	if normalized != "" {
		doc.TrailingNewline = strings.HasSuffix(normalized, "\n")
		body := normalized
		if doc.TrailingNewline {
			body = normalized[:len(normalized)-1]
		}
		lines = strings.Split(body, "\n")
	}

	global := &Section{Name: ""}
	doc.Sections = append(doc.Sections, global)
	current := global

	var pendingComments []string
	// seenKeys tracks key occurrences per logical (case-normalized) section
	// name for ErrorOnDuplicate detection. Keyed by normalized section name
	// so repeated "[Section]" blocks sharing a name share the same set,
	// matching findEntry's cross-block merge semantics.
	seenKeys := map[string]map[string]bool{}
	keyIndexFor := func(sectionName string) map[string]bool {
		norm := doc.normalize(sectionName)
		m, ok := seenKeys[norm]
		if !ok {
			m = map[string]bool{}
			seenKeys[norm] = m
		}
		return m
	}

	for i, line := range lines {
		lineNo := i + 1
		trimmed := strings.TrimSpace(line)

		if trimmed == "" {
			current.Entries = append(current.Entries, &Entry{
				Kind:            KindBlank,
				LeadingComments: pendingComments,
				OriginalLine:    line,
			})
			pendingComments = nil
			continue
		}

		if isCommentLine(trimmed, opts.CommentPrefixes) {
			pendingComments = append(pendingComments, line)
			continue
		}

		if strings.HasPrefix(trimmed, "[") && strings.HasSuffix(trimmed, "]") {
			name := strings.TrimSpace(trimmed[1 : len(trimmed)-1])
			sec := &Section{Name: name, LeadingComments: pendingComments, OriginalHeader: line}
			pendingComments = nil
			doc.Sections = append(doc.Sections, sec)
			current = sec
			continue
		}

		key, value, inline, ok := splitKeyValue(trimmed, opts.CommentPrefixes)
		if !ok || key == "" {
			current.Entries = append(current.Entries, &Entry{
				Kind:            KindRaw,
				LeadingComments: pendingComments,
				OriginalLine:    line,
			})
			pendingComments = nil
			continue
		}

		if opts.DuplicateKeyPolicy == ErrorOnDuplicate {
			idx := keyIndexFor(current.Name)
			normKey := doc.normalize(key)
			if idx[normKey] {
				return nil, &DuplicateKeyError{Section: current.Name, Key: key, Line: lineNo}
			}
			idx[normKey] = true
		}

		current.Entries = append(current.Entries, &Entry{
			Kind:            KindKeyValue,
			Key:             key,
			Value:           value,
			LeadingComments: pendingComments,
			InlineComment:   inline,
			OriginalLine:    line,
		})
		pendingComments = nil
	}

	if len(pendingComments) > 0 {
		doc.TrailingComments = pendingComments
	}

	return doc, nil
}

func isCommentLine(trimmed string, prefixes []rune) bool {
	if trimmed == "" {
		return false
	}
	r := []rune(trimmed)[0]
	for _, p := range prefixes {
		if r == p {
			return true
		}
	}
	return false
}

// splitKeyValue splits a trimmed non-comment, non-section line on the first
// "=", trimming both sides — the same rule as the confirmed
// ConvertFrom-BRAVOIniFile parser (IndexOf("="), Substring+Trim). ok is
// false if there is no "=" at all (the line becomes a KindRaw entry).
func splitKeyValue(trimmed string, prefixes []rune) (key, value, inline string, ok bool) {
	idx := strings.Index(trimmed, "=")
	if idx < 0 {
		return "", "", "", false
	}
	key = strings.TrimSpace(trimmed[:idx])
	rest := strings.TrimSpace(trimmed[idx+1:])
	value, inline = splitInlineComment(rest, prefixes)
	return key, value, inline, true
}

// splitInlineComment removes a trailing "<prefix> comment" from an
// already-trimmed value, but only when the prefix character is preceded by
// whitespace. This is a parser convenience beyond the confirmed production
// reader (which never strips inline comments and treats the full remainder
// as the value) so that editing a field through this tool does not clobber
// a trailing comment; it is safe for the schema's path-typed values since
// Windows paths do not contain ';' or '#' preceded by whitespace in
// practice. See Entry.InlineComment.
func splitInlineComment(value string, prefixes []rune) (string, string) {
	runes := []rune(value)
	for i := 1; i < len(runes); i++ {
		if unicode.IsSpace(runes[i-1]) && containsRune(prefixes, runes[i]) {
			v := strings.TrimSpace(string(runes[:i]))
			c := string(runes[i:])
			return v, c
		}
	}
	return value, ""
}

func containsRune(set []rune, r rune) bool {
	for _, s := range set {
		if s == r {
			return true
		}
	}
	return false
}
