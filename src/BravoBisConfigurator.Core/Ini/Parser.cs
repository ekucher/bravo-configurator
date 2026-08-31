using System.Text;

namespace BravoBisConfigurator.Core.Ini;

/// <summary>
///  Thrown by Parser.Parse when opts.DuplicateKeyPolicy is ErrorOnDuplicate
///  and a key repeats within the same logical section (including across two
///  separate "[Section]" blocks sharing a name).
/// </summary>
public sealed class DuplicateKeyException : Exception
{
    public string Section { get; }
    public string Key { get; }
    public int Line { get; }

    public DuplicateKeyException(string section, string key, int line)
        : base($"ini: duplicate key \"{key}\" in section \"{section}\" at line {line}")
    {
        Section = section;
        Key = key;
        Line = line;
    }
}

/// <summary>
///  Builds a Document from already-decoded (UTF-8) INI text. Ported 1:1
///  from internal/ini/parser.go — every line that isn't blank/comment/
///  section-header/key-value is kept as a verbatim Raw entry, and every
///  parsed line keeps its OriginalLine, so Writer.Write reproduces the
///  source exactly for anything the caller does not edit via Document.Set.
/// </summary>
public static class Parser
{
    public static Document Parse(string content, ParseOptions? opts = null)
    {
        if (opts is null || opts.CommentPrefixes.Length == 0)
        {
            opts = ParseOptions.Default();
        }

        var doc = new Document(opts.CaseInsensitiveKeys, opts.DuplicateKeyPolicy)
        {
            LineEnding = content.Contains("\r\n") ? "\r\n" : "\n",
        };

        var normalized = content.Replace("\r\n", "\n");

        List<string> lines = new();
        if (normalized != "")
        {
            doc.TrailingNewline = normalized.EndsWith("\n");
            var body = doc.TrailingNewline ? normalized[..^1] : normalized;
            lines = body.Split('\n').ToList();
        }

        var global = new Section { Name = "" };
        doc.Sections.Add(global);
        var current = global;

        List<string> pendingComments = new();
        // Tracks key occurrences per logical (case-normalized) section name
        // for ErrorOnDuplicate detection, keyed by normalized section name
        // so repeated "[Section]" blocks sharing a name share the same set.
        var seenKeys = new Dictionary<string, HashSet<string>>();
        HashSet<string> KeyIndexFor(string sectionName)
        {
            var norm = doc.Normalize(sectionName);
            if (!seenKeys.TryGetValue(norm, out var set))
            {
                set = new HashSet<string>();
                seenKeys[norm] = set;
            }
            return set;
        }

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var lineNo = i + 1;
            var trimmed = line.Trim();

            if (trimmed == "")
            {
                current.Entries.Add(new Entry
                {
                    Kind = EntryKind.Blank,
                    LeadingComments = pendingComments,
                    OriginalLine = line,
                });
                pendingComments = new List<string>();
                continue;
            }

            if (IsCommentLine(trimmed, opts.CommentPrefixes))
            {
                pendingComments.Add(line);
                continue;
            }

            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                var name = trimmed[1..^1].Trim();
                var sec = new Section { Name = name, LeadingComments = pendingComments, OriginalHeader = line };
                pendingComments = new List<string>();
                doc.Sections.Add(sec);
                current = sec;
                continue;
            }

            var (key, value, inline, ok) = SplitKeyValue(trimmed, opts.CommentPrefixes);
            if (!ok || key == "")
            {
                current.Entries.Add(new Entry
                {
                    Kind = EntryKind.Raw,
                    LeadingComments = pendingComments,
                    OriginalLine = line,
                });
                pendingComments = new List<string>();
                continue;
            }

            if (opts.DuplicateKeyPolicy == DuplicateKeyPolicy.ErrorOnDuplicate)
            {
                var idx = KeyIndexFor(current.Name);
                var normKey = doc.Normalize(key);
                if (!idx.Add(normKey))
                {
                    throw new DuplicateKeyException(current.Name, key, lineNo);
                }
            }

            current.Entries.Add(new Entry
            {
                Kind = EntryKind.KeyValue,
                Key = key,
                Value = value,
                LeadingComments = pendingComments,
                InlineComment = inline,
                OriginalLine = line,
            });
            pendingComments = new List<string>();
        }

        if (pendingComments.Count > 0)
        {
            doc.TrailingComments = pendingComments;
        }

        return doc;
    }

    private static bool IsCommentLine(string trimmed, char[] prefixes)
    {
        if (trimmed == "")
        {
            return false;
        }
        return prefixes.Contains(trimmed[0]);
    }

    /// <summary>
    ///  Splits a trimmed non-comment, non-section line on the first "=",
    ///  trimming both sides — the same rule as the confirmed
    ///  ConvertFrom-BRAVOIniFile parser (IndexOf("="), Substring+Trim). ok
    ///  is false if there is no "=" at all (the line becomes a Raw entry).
    /// </summary>
    private static (string key, string value, string inline, bool ok) SplitKeyValue(string trimmed, char[] prefixes)
    {
        var idx = trimmed.IndexOf('=');
        if (idx < 0)
        {
            return ("", "", "", false);
        }
        var key = trimmed[..idx].Trim();
        var rest = trimmed[(idx + 1)..].Trim();
        var (value, inline) = SplitInlineComment(rest, prefixes);
        return (key, value, inline, true);
    }

    /// <summary>
    ///  Removes a trailing "&lt;prefix&gt; comment" from an already-trimmed
    ///  value, but only when the prefix character is preceded by
    ///  whitespace. This is a parser convenience beyond the confirmed
    ///  production reader (which never strips inline comments) so that
    ///  editing a field through this tool does not clobber a trailing
    ///  comment; see Entry.InlineComment.
    /// </summary>
    private static (string value, string comment) SplitInlineComment(string value, char[] prefixes)
    {
        for (var i = 1; i < value.Length; i++)
        {
            if (char.IsWhiteSpace(value[i - 1]) && prefixes.Contains(value[i]))
            {
                var v = value[..i].Trim();
                var c = value[i..];
                return (v, c);
            }
        }
        return (value, "");
    }
}
