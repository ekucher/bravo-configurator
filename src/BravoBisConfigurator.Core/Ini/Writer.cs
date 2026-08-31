using System.Text;

namespace BravoBisConfigurator.Core.Ini;

/// <summary>
///  Serializes a Document back to text. Ported 1:1 from
///  internal/ini/writer.go: every line the caller did not modify (via
///  Document.Set, or a brand-new section/entry) is re-emitted exactly as
///  parsed — same content, casing, spacing, comments and blank-line
///  placement — using doc.LineEnding for every line and stripping the final
///  terminator when doc.TrailingNewline is false, so that opening a file and
///  saving it again without any edits reproduces it byte-for-byte.
/// </summary>
public static class Writer
{
    public static string Write(Document doc)
    {
        var nl = string.IsNullOrEmpty(doc.LineEnding) ? "\n" : doc.LineEnding;

        var b = new StringBuilder();
        void WriteComments(IEnumerable<string> cs)
        {
            foreach (var c in cs)
            {
                b.Append(c);
                b.Append(nl);
            }
        }

        foreach (var sec in doc.Sections)
        {
            WriteComments(sec.LeadingComments);
            if (sec.Name != "")
            {
                b.Append(sec.OriginalHeader != "" ? sec.OriginalHeader : $"[{sec.Name}]");
                b.Append(nl);
            }
            foreach (var e in sec.Entries)
            {
                WriteComments(e.LeadingComments);
                switch (e.Kind)
                {
                    case EntryKind.Blank:
                    case EntryKind.Raw:
                        b.Append(e.OriginalLine);
                        break;
                    case EntryKind.KeyValue:
                        if (!e.Dirty && e.OriginalLine != "")
                        {
                            b.Append(e.OriginalLine);
                        }
                        else
                        {
                            b.Append(e.Key);
                            b.Append('=');
                            b.Append(e.Value);
                            if (e.InlineComment != "")
                            {
                                b.Append(' ');
                                b.Append(e.InlineComment);
                            }
                        }
                        break;
                }
                b.Append(nl);
            }
        }
        WriteComments(doc.TrailingComments);

        var result = b.ToString();
        if (!doc.TrailingNewline && result.EndsWith(nl))
        {
            result = result[..^nl.Length];
        }
        return result;
    }
}
