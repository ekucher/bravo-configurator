package ini

import "strings"

// Write serializes doc back to text. Every line the caller did not modify
// (via Document.Set, or a brand-new section/entry) is re-emitted exactly as
// parsed — same content, casing, spacing, comments and blank-line
// placement — using doc.LineEnding for every line and stripping the final
// terminator when doc.TrailingNewline is false, so that opening a file and
// saving it again without any edits reproduces it byte-for-byte.
func Write(doc *Document) string {
	nl := doc.LineEnding
	if nl == "" {
		nl = "\n"
	}

	var b strings.Builder
	writeComments := func(cs []string) {
		for _, c := range cs {
			b.WriteString(c)
			b.WriteString(nl)
		}
	}

	for _, sec := range doc.Sections {
		writeComments(sec.LeadingComments)
		if sec.Name != "" {
			if sec.OriginalHeader != "" {
				b.WriteString(sec.OriginalHeader)
			} else {
				b.WriteString("[" + sec.Name + "]")
			}
			b.WriteString(nl)
		}
		for _, e := range sec.Entries {
			writeComments(e.LeadingComments)
			switch e.Kind {
			case KindBlank, KindRaw:
				b.WriteString(e.OriginalLine)
			case KindKeyValue:
				if !e.Dirty && e.OriginalLine != "" {
					b.WriteString(e.OriginalLine)
				} else {
					b.WriteString(e.Key)
					b.WriteString("=")
					b.WriteString(e.Value)
					if e.InlineComment != "" {
						b.WriteString(" ")
						b.WriteString(e.InlineComment)
					}
				}
			}
			b.WriteString(nl)
		}
	}
	writeComments(doc.TrailingComments)

	out := b.String()
	if !doc.TrailingNewline {
		out = strings.TrimSuffix(out, nl)
	}
	return out
}
