using BravoBisConfigurator.Core.Ini;
using BravoBisConfigurator.Core.Locate;
using BravoBisConfigurator.Core.Profile;
using BravoBisConfigurator.Core.Validate;
// Aliased directly (not "using BravoBisConfigurator.Core.Schema;") because
// FormModel declares an instance property named "Schema" (below), which
// shadows the namespace for any bare "Schema.X" reference from a static
// method in this class — see ComputeResolvedHint.
using FieldType = BravoBisConfigurator.Core.Schema.FieldType;

namespace BravoBisConfigurator.Core.Model;

/// <summary>
///  Everything one editor screen needs: which profile/schema is active, the
///  live Document being edited, and the section/field views (with findings)
///  derived from the current state of that document. Ported 1:1 from
///  internal/app/model.go.
/// </summary>
public sealed partial class FormModel
{
    public ProfileDefinition Profile { get; }
    public Schema.Schema Schema { get; }
    public Document Doc { get; }
    public IniEncoding Encoding { get; }
    public string FilePath { get; }

    public List<SectionView> Sections { get; private set; } = new();
    public List<Result> AllFindings { get; private set; } = new();

    /// <summary>
    ///  Best-effort inferred LIMS installation directory used only to
    ///  compute FieldView.ResolvedHint — "" when it can't be determined.
    ///  See InstallRoot() for how each profile derives it.
    /// </summary>
    public string InstallRoot { get; private set; } = "";

    /// <summary>
    ///  Indirects LocateService so tests can substitute deterministic
    ///  values without touching the real system directory or exe path —
    ///  the C# equivalent of internal/app/save.go's
    ///  systemBravoIniPathFunc/executableDirFunc package-level var
    ///  indirection, expressed as constructor-injected delegates instead of
    ///  hidden package state. Deliberately constructor parameters (not
    ///  init-only properties set via object-initializer syntax): the
    ///  constructor's own Refresh() call already needs ExecutableDirFunc
    ///  for a "bis"-profile InstallRoot — an object-initializer's property
    ///  assignments run strictly after the constructor body finishes, which
    ///  would be too late for that first Refresh().
    /// </summary>
    public Func<string> SystemBravoIniPathFunc { get; }

    public Func<string> ExecutableDirFunc { get; }

    public FormModel(
        ProfileDefinition profile,
        Schema.Schema schema,
        Document doc,
        IniEncoding encoding,
        string filePath,
        Func<string>? systemBravoIniPathFunc = null,
        Func<string>? executableDirFunc = null)
    {
        SystemBravoIniPathFunc = systemBravoIniPathFunc ?? (() => LocateService.SystemBravoIniPath());
        ExecutableDirFunc = executableDirFunc ?? LocateService.ExecutableDir;
        Profile = profile;
        Schema = schema;
        Doc = doc;
        Encoding = encoding;
        FilePath = filePath;
        Refresh();
    }

    /// <summary>
    ///  Recomputes AllFindings and rebuilds Sections from the current state
    ///  of Doc. Called once at construction and again after every edit.
    /// </summary>
    private void Refresh()
    {
        AllFindings = Engine.Validate(Doc, Schema);
        InstallRoot = ComputeInstallRoot();

        var sections = new List<SectionView>(Schema.Sections.Count);
        foreach (var sec in Schema.Sections)
        {
            var fields = new List<FieldView>(sec.Fields.Count);
            foreach (var f in sec.Fields)
            {
                var value = Doc.Get(sec.Name, f.Key);
                fields.Add(new FieldView
                {
                    Section = sec.Name,
                    Key = f.Key,
                    Label = f.Label,
                    Description = f.Description,
                    Type = f.Type,
                    Value = value,
                    Required = f.Required,
                    Rule = f.Validation,
                    Findings = FindingsFor(AllFindings, sec.Name, f.Key),
                    ResolvedHint = ComputeResolvedHint(f.Type, value, InstallRoot),
                });
            }
            sections.Add(new SectionView { Name = sec.Name, Label = sec.Label, Description = sec.Description, Fields = fields });
        }
        Sections = sections;
    }

    /// <summary>
    ///  Best-effort infers the LIMS installation directory used to resolve
    ///  relative path fields into an informational absolute hint — never
    ///  written back to the document.
    ///
    ///  - "bravo": inferred from bravo.ini's own [model] BLOG value
    ///    (falling back to BEXCH), since the real server install directory
    ///    is the parent of those confirmed-active storage directories.
    ///    Deliberately does not query the bravo.exe Windows service's own
    ///    executable path (what BRAVO-Toolkit's canonical
    ///    Get-BRAVOInstallationRoot logic uses) — this is a lighter,
    ///    self-contained heuristic appropriate for a standalone display
    ///    hint, not a claim of parity with that canonical resolution.
    ///  - everything else ("bis" today): the running configurator.exe's own
    ///    directory — bis.exe/bis.ini live in that same LIMS client install
    ///    directory by this tool's own deployment convention.
    /// </summary>
    private string ComputeInstallRoot()
    {
        if (Profile.Name == "bravo")
        {
            foreach (var key in new[] { "BLOG", "BEXCH" })
            {
                if (Doc.TryGet("model", key, out var value))
                {
                    var root = ParentOfAbsolutePath(value);
                    if (root != "")
                    {
                        return root;
                    }
                }
            }
            return "";
        }
        try
        {
            return ExecutableDirFunc();
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    ///  Path.GetDirectoryName(path) with any trailing separator trimmed
    ///  first, or "" if path isn't an absolute Windows path.
    /// </summary>
    private static string ParentOfAbsolutePath(string path)
    {
        var trimmed = path.TrimEnd('\\', '/');
        if (!IsAbsoluteWindowsPath(trimmed))
        {
            return "";
        }
        return Path.GetDirectoryName(trimmed) ?? "";
    }

    /// <summary>
    ///  Whether path starts with a drive letter ("C:\...") or a UNC prefix
    ///  ("\\server\share").
    /// </summary>
    private static bool IsAbsoluteWindowsPath(string path)
    {
        if (path.Length >= 2 && path[1] == ':')
        {
            return true;
        }
        return path.StartsWith(@"\\");
    }

    /// <summary>
    ///  Computes FieldView.ResolvedHint: only for a path-typed field with a
    ///  non-empty, relative value, while root is known.
    /// </summary>
    private static string ComputeResolvedHint(FieldType t, string value, string root)
    {
        if (t != FieldType.Path || value == "" || root == "")
        {
            return "";
        }
        if (IsAbsoluteWindowsPath(value))
        {
            return "";
        }
        return Path.Combine(root, value);
    }

    private static List<Result> FindingsFor(List<Result> findings, string section, string key) =>
        findings.Where(r => string.Equals(r.Section, section, StringComparison.OrdinalIgnoreCase)
                          && string.Equals(r.Key, key, StringComparison.OrdinalIgnoreCase))
                .ToList();

    /// <summary>
    ///  Writes value to (section, key) in the underlying document and
    ///  recomputes findings/section views, so the GUI can re-render inline
    ///  errors immediately after each field change.
    /// </summary>
    public void ApplyEdit(string section, string key, string value)
    {
        Doc.Set(section, key, value);
        Refresh();
    }

    /// <summary>
    ///  Whether the current state has zero Severity.Error findings. The
    ///  GUI disables its Save button while this is false.
    /// </summary>
    public bool CanSave() => !Engine.HasErrors(AllFindings);

    /// <summary>
    ///  The subset of AllFindings for keys the schema doesn't define at all
    ///  (as opposed to a known field failing its own rule) — used to
    ///  render a separate "unrecognized keys" panel.
    /// </summary>
    public List<Result> UnrecognizedFindings() =>
        AllFindings.Where(r => !Schema.FindField(r.Section, r.Key, out _, out _)).ToList();
}
