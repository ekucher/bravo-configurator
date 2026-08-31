namespace BravoBisConfigurator.Core.Ini;

/// <summary>
///  Distinguishes what kind of physical line an <see cref="Entry"/> represents.
/// </summary>
public enum EntryKind
{
    /// <summary>A parsed "Key=Value" line.</summary>
    KeyValue,
    /// <summary>A preserved blank line (whitespace-only or empty).</summary>
    Blank,
    /// <summary>
    ///  A line the parser could not classify as blank, a comment, a
    ///  "[Section]" header, or "Key=Value" — kept verbatim so nothing the
    ///  tool doesn't understand is ever silently dropped on save.
    /// </summary>
    Raw,
}

/// <summary>
///  One physical line inside a <see cref="Section"/> (a key/value pair, a
///  blank line, or an unclassified raw line), plus any comment lines that
///  preceded it in the source. Ported 1:1 from internal/ini/document.go's
///  Entry (see that file's doc comments for the full round-trip rationale).
/// </summary>
public sealed class Entry
{
    public EntryKind Kind { get; set; }

    /// <summary>Set only for KeyValue; original casing as first parsed.</summary>
    public string Key { get; set; } = "";

    /// <summary>Set only for KeyValue.</summary>
    public string Value { get; set; } = "";

    /// <summary>
    ///  Full original comment lines (including the prefix character and
    ///  original indentation) immediately preceding this entry in the source.
    /// </summary>
    public List<string> LeadingComments { get; set; } = new();

    /// <summary>
    ///  A trailing "; ..." (prefix included) found on the same line as a
    ///  KeyValue entry, split off only when the prefix character is preceded
    ///  by whitespace (see Parser.SplitInlineComment).
    /// </summary>
    public string InlineComment { get; set; } = "";

    /// <summary>
    ///  The exact original source line (no line terminator). Writer.Write
    ///  re-emits it verbatim while Dirty is false, guaranteeing
    ///  byte-identical round-trip for anything the caller did not change.
    /// </summary>
    public string OriginalLine { get; set; } = "";

    /// <summary>
    ///  Set by Document.Set when a KeyValue entry's value is changed,
    ///  forcing Writer.Write to regenerate "Key=Value[ ; comment]" for this
    ///  line instead of reusing OriginalLine.
    /// </summary>
    public bool Dirty { get; set; }
}
