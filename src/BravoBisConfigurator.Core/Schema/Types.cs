using System.Runtime.Serialization;
using YamlDotNet.Serialization;

namespace BravoBisConfigurator.Core.Schema;

/// <summary>
///  The kind of value a FieldDef holds, used to pick a GUI widget and a
///  type-coercion rule in Validate. Ported 1:1 from internal/schema/types.go
///  — string values match the YAML on disk exactly (bravo.schema.yaml /
///  bis.schema.yaml are unchanged by this port). Enum members are mapped to
///  their YAML string via EnumMemberAttribute, which YamlDotNet honors
///  natively — not YamlMemberAttribute, which only applies to properties.
/// </summary>
public enum FieldType
{
    [EnumMember(Value = "string")] String,
    [EnumMember(Value = "path")] Path,
    [EnumMember(Value = "enum")] Enum,
    [EnumMember(Value = "int")] Int,
    [EnumMember(Value = "float")] Float,
    [EnumMember(Value = "bool")] Bool,
}

/// <summary>
///  Controls whether a failed ValidationRule blocks saving (Error) or is
///  merely surfaced to the operator (Warning).
/// </summary>
public enum Severity
{
    [EnumMember(Value = "error")] Error,
    [EnumMember(Value = "warning")] Warning,
}

/// <summary>Selects which check ValidationRule performs.</summary>
public enum RuleKind
{
    [EnumMember(Value = "path-exists")] PathExists,
    [EnumMember(Value = "regex")] Regex,
    [EnumMember(Value = "enum")] Enum,
    [EnumMember(Value = "range")] Range,
}

/// <summary>
///  Narrows RulePathExists to files, directories, or either. Unspecified has
///  no YAML representation — path_mode is simply absent from the mapping
///  when unset, leaving this property at its C# default (Unspecified),
///  mirroring the Go PathMode zero value "".
/// </summary>
public enum PathMode
{
    Unspecified,
    [EnumMember(Value = "file")] File,
    [EnumMember(Value = "dir")] Dir,
    [EnumMember(Value = "either")] Either,
}

/// <summary>One check attached to a FieldDef.</summary>
public sealed class ValidationRule
{
    [YamlMember(Alias = "kind")] public RuleKind Kind { get; set; }
    [YamlMember(Alias = "pattern")] public string? Pattern { get; set; } // RuleRegex
    [YamlMember(Alias = "values")] public List<string>? Values { get; set; } // RuleEnum
    [YamlMember(Alias = "min")] public double? Min { get; set; } // RuleRange
    [YamlMember(Alias = "max")] public double? Max { get; set; } // RuleRange
    [YamlMember(Alias = "path_mode")] public PathMode PathMode { get; set; } = PathMode.Unspecified; // RulePathExists

    /// <summary>Severity defaults to Error when unset (see the YAML: SeverityRaw is null until set).</summary>
    [YamlMember(Alias = "severity")] public Severity? SeverityRaw { get; set; }

    public Severity EffectiveSeverity() => SeverityRaw ?? Severity.Error;
}

/// <summary>Describes one INI key within a SectionDef.</summary>
public sealed class FieldDef
{
    [YamlMember(Alias = "key")] public string Key { get; set; } = "";
    [YamlMember(Alias = "label")] public string Label { get; set; } = "";
    [YamlMember(Alias = "type")] public FieldType Type { get; set; }
    [YamlMember(Alias = "required")] public bool Required { get; set; }
    [YamlMember(Alias = "default")] public string? Default { get; set; }
    [YamlMember(Alias = "description")] public string? Description { get; set; }
    [YamlMember(Alias = "validation")] public ValidationRule? Validation { get; set; }
}

/// <summary>Describes one INI "[Name]" section and the fields within it.</summary>
public sealed class SectionDef
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = "";
    [YamlMember(Alias = "label")] public string Label { get; set; } = "";
    [YamlMember(Alias = "description")] public string? Description { get; set; }
    [YamlMember(Alias = "fields")] public List<FieldDef> Fields { get; set; } = new();
}

/// <summary>
///  Whole-schema honesty marker, surfaced by the GUI as a persistent banner
///  and used to decide default field severities.
/// </summary>
public enum SchemaStatus
{
    [EnumMember(Value = "draft")] Draft,
    [EnumMember(Value = "verified")] Verified,
}

/// <summary>One profile's full field catalog, as loaded from YAML.</summary>
public sealed class Schema
{
    [YamlMember(Alias = "profile")] public string ProfileName { get; set; } = "";
    [YamlMember(Alias = "version")] public string Version { get; set; } = "";
    [YamlMember(Alias = "status")] public SchemaStatus Status { get; set; }
    [YamlMember(Alias = "sections")] public List<SectionDef> Sections { get; set; } = new();

    /// <summary>
    ///  The FieldDef for (sectionName, key), matching case-insensitively
    ///  (INI section/key names are case-insensitive per Ini's default
    ///  rules).
    /// </summary>
    public bool FindField(string sectionName, string key, out FieldDef? field, out SectionDef? section)
    {
        foreach (var sec in Sections)
        {
            if (!string.Equals(sec.Name, sectionName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            foreach (var f in sec.Fields)
            {
                if (string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    field = f;
                    section = sec;
                    return true;
                }
            }
        }
        field = null;
        section = null;
        return false;
    }
}
