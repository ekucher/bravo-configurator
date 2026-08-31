namespace BravoBisConfigurator.Core.Ini;

/// <summary>A flattened (section, key, value) triple.</summary>
public readonly record struct KeyValue(string Section, string Key, string Value);

/// <summary>
///  A parsed INI file: an ordered list of physical <see cref="Section"/>s
///  plus any comments trailing the last entry, together with enough
///  metadata (LineEnding, TrailingNewline, and the comparison rules from
///  ParseOptions) to reproduce the source exactly via Writer.Write when
///  nothing was edited. Ported 1:1 from internal/ini/document.go.
/// </summary>
public sealed class Document
{
    public List<Section> Sections { get; set; } = new();
    public List<string> TrailingComments { get; set; } = new();

    /// <summary>
    ///  "\r\n" or "\n", detected from the source. Mixed line endings in the
    ///  source are normalized to this single value on Write.
    /// </summary>
    public string LineEnding { get; set; } = "\n";

    /// <summary>
    ///  Whether the source file's last line ended with a line terminator,
    ///  so Write reproduces that exactly.
    /// </summary>
    public bool TrailingNewline { get; set; }

    internal bool CaseInsensitive { get; init; }
    internal DuplicateKeyPolicy DupPolicy { get; init; }

    public Document(bool caseInsensitive, DuplicateKeyPolicy dupPolicy)
    {
        CaseInsensitive = caseInsensitive;
        DupPolicy = dupPolicy;
    }

    /// <summary>
    ///  Every KeyValue (section, key, value) triple in the document, in
    ///  file order, including duplicates across repeated "[Section]" blocks.
    /// </summary>
    public List<KeyValue> AllEntries()
    {
        var result = new List<KeyValue>();
        foreach (var sec in Sections)
        {
            foreach (var e in sec.Entries)
            {
                if (e.Kind == EntryKind.KeyValue)
                {
                    result.Add(new KeyValue(sec.Name, e.Key, e.Value));
                }
            }
        }
        return result;
    }

    /// <summary>
    ///  The distinct section names in the document, in the order each was
    ///  first seen, merging repeated physical blocks that share a name.
    /// </summary>
    public List<string> LogicalSectionNames()
    {
        var seen = new HashSet<string>();
        var result = new List<string>();
        foreach (var s in Sections)
        {
            var norm = Normalize(s.Name);
            if (!seen.Add(norm))
            {
                continue;
            }
            result.Add(s.Name);
        }
        return result;
    }

    internal string Normalize(string s) => CaseInsensitive ? s.ToLowerInvariant() : s;

    private bool SectionEquals(string a, string b) => Normalize(a) == Normalize(b);

    private bool KeyEquals(string a, string b) => Normalize(a) == Normalize(b);

    /// <summary>Every physical Section block whose name matches name, in file order.</summary>
    internal List<Section> MatchingSections(string name) =>
        Sections.Where(s => SectionEquals(s.Name, name)).ToList();

    /// <summary>
    ///  Resolves the effective entry for (section, key) across every
    ///  physical block sharing that section name, honoring DupPolicy the
    ///  same way whether the duplicate is two keys in one block or the same
    ///  key repeated across two separate "[Section]" blocks.
    /// </summary>
    private (Entry? entry, Section? section) FindEntry(string section, string key)
    {
        Entry? match = null;
        Section? matchSec = null;
        foreach (var sec in MatchingSections(section))
        {
            foreach (var e in sec.Entries)
            {
                if (e.Kind != EntryKind.KeyValue || !KeyEquals(e.Key, key))
                {
                    continue;
                }
                match = e;
                matchSec = sec;
                if (DupPolicy == DuplicateKeyPolicy.FirstWins)
                {
                    return (match, matchSec);
                }
            }
        }
        return (match, matchSec);
    }

    /// <summary>
    ///  The effective value for section/key, resolving duplicates per
    ///  DupPolicy. Returns false if the section or key is absent.
    /// </summary>
    public bool TryGet(string section, string key, out string value)
    {
        var (e, _) = FindEntry(section, key);
        if (e is null)
        {
            value = "";
            return false;
        }
        value = e.Value;
        return true;
    }

    /// <summary>Convenience wrapper over TryGet returning "" when absent.</summary>
    public string Get(string section, string key) => TryGet(section, key, out var v) ? v : "";

    /// <summary>
    ///  The first physical block named name, creating a new (initially
    ///  empty, header-less) block appended at the end of the document if
    ///  none exists yet.
    /// </summary>
    public Section EnsureSection(string name)
    {
        var secs = MatchingSections(name);
        if (secs.Count > 0)
        {
            return secs[0];
        }
        var sec = new Section { Name = name };
        Sections.Add(sec);
        return sec;
    }

    /// <summary>
    ///  Writes value for section/key. If the key already exists (in any
    ///  physical block sharing the section name), the entry Get would
    ///  return is updated in place and marked Dirty so Write regenerates
    ///  that one line; every other line is left byte-for-byte untouched. If
    ///  the key does not exist yet, it is appended to the last physical
    ///  block with that section name (creating the section, at the end of
    ///  the document, if none exists at all).
    /// </summary>
    public void Set(string section, string key, string value)
    {
        var (e, _) = FindEntry(section, key);
        if (e is not null)
        {
            e.Value = value;
            e.Dirty = true;
            return;
        }
        var secs = MatchingSections(section);
        var target = secs.Count > 0 ? secs[^1] : EnsureSection(section);
        target.Entries.Add(new Entry { Kind = EntryKind.KeyValue, Key = key, Value = value, Dirty = true });
    }
}
