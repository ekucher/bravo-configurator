namespace BravoBisConfigurator.Core.Ini;

/// <summary>
///  One physical "[Name]" block in the source file (or, for Name == "", the
///  implicit global block preceding the first header). A section name may
///  appear more than once in a file as separate physical Section blocks;
///  Document.Get/Set treat all blocks sharing a name as one logical section
///  (see Document.MatchingSections), while Writer keeps each block in its
///  original file position. Ported 1:1 from internal/ini/document.go.
/// </summary>
public sealed class Section
{
    public string Name { get; set; } = "";
    public List<string> LeadingComments { get; set; } = new();

    /// <summary>
    ///  The exact original "[Name]" source line. Empty for the implicit
    ///  global section and for sections created by Set/EnsureSection that
    ///  did not exist in the source file.
    /// </summary>
    public string OriginalHeader { get; set; } = "";

    public List<Entry> Entries { get; set; } = new();

    /// <summary>All KeyValue keys in this section, in file order (duplicates included).</summary>
    public IEnumerable<string> Keys() => Entries.Where(e => e.Kind == EntryKind.KeyValue).Select(e => e.Key);
}
