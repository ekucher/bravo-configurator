using BravoBisConfigurator.Core.Ini;
using BravoBisConfigurator.Core.Schema;

namespace BravoBisConfigurator.Core.Validate;

/// <summary>
///  Checks a parsed Document against a Schema: required fields, type
///  coercion, and per-field ValidationRule checks (path-exists/regex/enum/
///  range), plus a warning for any key present in the file that the schema
///  doesn't know about. Ported 1:1 from internal/validate/engine.go.
/// </summary>
public static class Engine
{
    /// <summary>Whether results contains at least one Severity.Error finding.</summary>
    public static bool HasErrors(IEnumerable<Result> results) => results.Any(r => r.Severity == Severity.Error);

    /// <summary>
    ///  Checks doc against s and returns every finding, schema fields first
    ///  (in schema order), then unrecognized-key warnings (in file order).
    /// </summary>
    public static List<Result> Validate(Document doc, Schema.Schema s)
    {
        var results = new List<Result>();

        foreach (var sec in s.Sections)
        {
            foreach (var f in sec.Fields)
            {
                results.AddRange(ValidateField(doc, sec.Name, f));
            }
        }

        results.AddRange(UnknownKeyWarnings(doc, s));

        return results;
    }

    private static List<Result> ValidateField(Document doc, string sectionName, FieldDef f)
    {
        if (!doc.TryGet(sectionName, f.Key, out var value))
        {
            if (f.Required)
            {
                return new List<Result>
                {
                    new(sectionName, f.Key, Severity.Error, "required field is missing"),
                };
            }
            return new List<Result>();
        }

        var results = new List<Result>();

        var (typeMsg, typeFailed) = Rules.TypeCoercionError(f.Type, value);
        if (typeFailed)
        {
            results.Add(new Result(sectionName, f.Key, Severity.Error, typeMsg));
            // A value that doesn't even match its declared type can't be
            // meaningfully checked against a further rule (e.g. a range
            // rule on a non-numeric string); stop here for this field.
            return results;
        }

        if (f.Validation is not null)
        {
            var (msg, failed) = Rules.CheckRule(f.Validation, value);
            if (failed)
            {
                results.Add(new Result(sectionName, f.Key, f.Validation.EffectiveSeverity(), msg));
            }
        }

        return results;
    }

    /// <summary>
    ///  Flags every key physically present in doc that the schema has no
    ///  FieldDef for, deduplicated by (section, key) so a duplicated
    ///  unknown key in the source file produces one warning, not one per
    ///  physical occurrence.
    /// </summary>
    private static List<Result> UnknownKeyWarnings(Document doc, Schema.Schema s)
    {
        var seen = new HashSet<string>();
        var results = new List<Result>();
        foreach (var kv in doc.AllEntries())
        {
            if (s.FindField(kv.Section, kv.Key, out _, out _))
            {
                continue;
            }
            var dedupKey = NormalizeDedupKey(kv.Section, kv.Key);
            if (!seen.Add(dedupKey))
            {
                continue;
            }
            results.Add(new Result(kv.Section, kv.Key, Severity.Warning, "unrecognized key — preserved on save, not validated"));
        }
        return results;
    }

    private static string NormalizeDedupKey(string section, string key) =>
        $"{section.ToLowerInvariant()}\0{key.ToLowerInvariant()}";
}
