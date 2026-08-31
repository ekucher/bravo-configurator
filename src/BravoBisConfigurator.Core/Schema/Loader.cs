using System.Reflection;
using YamlDotNet.Serialization;

namespace BravoBisConfigurator.Core.Schema;

/// <summary>
///  Loads the declarative field definitions that drive both Validate and
///  the GUI form generator. Ported 1:1 from internal/schema/loader.go.
/// </summary>
public static class Loader
{
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder().Build();

    /// <summary>Loads the bundled default schema for profile ("bravo" or "bis").</summary>
    public static Schema LoadEmbedded(string profile)
    {
        var logicalName = $"BravoBisConfigurator.Core.Schema.Defaults.{profile}.schema.yaml";
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(logicalName)
            ?? throw new InvalidOperationException($"schema: no bundled schema for profile \"{profile}\"");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    /// <summary>
    ///  Reads and validates a schema YAML file from disk — used when an
    ///  operator supplies a corrected/real schema without rebuilding the
    ///  tool.
    /// </summary>
    public static Schema Load(string path)
    {
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"schema: read {path}: {ex.Message}", ex);
        }
        return Parse(text);
    }

    private static Schema Parse(string yamlText)
    {
        Schema s;
        try
        {
            s = YamlDeserializer.Deserialize<Schema>(yamlText) ?? new Schema();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"schema: parse YAML: {ex.Message}", ex);
        }
        ValidateSchemaShape(s);
        return s;
    }

    /// <summary>
    ///  Checks the schema document itself is well-formed (distinct from
    ///  Validate, which checks an ini.Document against an already-loaded
    ///  Schema): every field has a key/type, every rule kind is recognized,
    ///  enum/range/regex rules carry the data they need.
    /// </summary>
    private static void ValidateSchemaShape(Schema s)
    {
        if (string.IsNullOrEmpty(s.ProfileName))
        {
            throw new InvalidOperationException("schema: missing required top-level \"profile\"");
        }
        if (s.Status is not (SchemaStatus.Draft or SchemaStatus.Verified))
        {
            throw new InvalidOperationException($"schema {s.ProfileName}: invalid status \"{s.Status}\"");
        }
        foreach (var sec in s.Sections)
        {
            if (string.IsNullOrEmpty(sec.Name))
            {
                throw new InvalidOperationException($"schema {s.ProfileName}: a section is missing \"name\"");
            }
            foreach (var f in sec.Fields)
            {
                ValidateFieldShape(s.ProfileName, sec.Name, f);
            }
        }
    }

    private static void ValidateFieldShape(string profile, string section, FieldDef f)
    {
        if (string.IsNullOrEmpty(f.Key))
        {
            throw new InvalidOperationException($"schema {profile}: section \"{section}\" has a field with no \"key\"");
        }
        // f.Type is a real C# enum, so an out-of-range YAML string already
        // fails during Deserialize<Schema>() above — no further type check
        // needed here (unlike Go, where FieldType is a plain string).

        var v = f.Validation;
        if (v is null)
        {
            return;
        }
        switch (v.Kind)
        {
            case RuleKind.PathExists:
                // v.PathMode is a real C# enum; any value it can hold is valid.
                break;
            case RuleKind.Regex:
                if (string.IsNullOrEmpty(v.Pattern))
                {
                    throw new InvalidOperationException($"schema {profile}: {section}.{f.Key}: regex rule requires \"pattern\"");
                }
                break;
            case RuleKind.Enum:
                if (v.Values is null || v.Values.Count == 0)
                {
                    throw new InvalidOperationException($"schema {profile}: {section}.{f.Key}: enum rule requires \"values\"");
                }
                break;
            case RuleKind.Range:
                if (v.Min is null && v.Max is null)
                {
                    throw new InvalidOperationException($"schema {profile}: {section}.{f.Key}: range rule requires \"min\" and/or \"max\"");
                }
                break;
        }
    }
}
