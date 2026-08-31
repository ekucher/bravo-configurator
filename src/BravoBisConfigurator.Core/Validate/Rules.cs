using System.Text.RegularExpressions;
using BravoBisConfigurator.Core.Schema;

namespace BravoBisConfigurator.Core.Validate;

/// <summary>
///  Type-coercion and ValidationRule checks. Ported 1:1 from
///  internal/validate/rules.go.
/// </summary>
internal static class Rules
{
    /// <summary>
    ///  Checks that value can be interpreted as t, returning (message, true)
    ///  if not. String/Path/Enum accept any string — enum membership is
    ///  enforced separately by a RuleEnum, not by the type itself, since not
    ///  every enum-typed field necessarily has a rule attached in every
    ///  schema.
    /// </summary>
    public static (string message, bool failed) TypeCoercionError(FieldType t, string value)
    {
        switch (t)
        {
            case FieldType.Int:
                if (!long.TryParse(value.Trim(), out _))
                {
                    return ($"value \"{value}\" is not a valid integer", true);
                }
                break;
            case FieldType.Float:
                if (!double.TryParse(value.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
                {
                    return ($"value \"{value}\" is not a valid number", true);
                }
                break;
            case FieldType.Bool:
                if (!TryParseBool(value, out _))
                {
                    return ($"value \"{value}\" is not a valid boolean (expected 0/1/true/false)", true);
                }
                break;
            case FieldType.String:
            case FieldType.Path:
            case FieldType.Enum:
                // Any string is structurally valid; semantic checks are rule-driven.
                break;
        }
        return ("", false);
    }

    /// <summary>
    ///  Accepts the value conventions actually observed in the real
    ///  bravo.ini/bis.ini samples (0/1) plus the common true/false spelling,
    ///  all case-insensitively.
    /// </summary>
    private static bool TryParseBool(string value, out bool result)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "0":
            case "false":
                result = false;
                return true;
            case "1":
            case "true":
                result = true;
                return true;
            default:
                result = false;
                return false;
        }
    }

    /// <summary>Runs r against value, returning (message, true) if it fails.</summary>
    public static (string message, bool failed) CheckRule(ValidationRule r, string value) => r.Kind switch
    {
        RuleKind.PathExists => CheckPathExists(r, value),
        RuleKind.Regex => CheckRegex(r, value),
        RuleKind.Enum => CheckEnum(r, value),
        RuleKind.Range => CheckRange(r, value),
        _ => ($"internal error: unknown validation rule kind \"{r.Kind}\"", true),
    };

    private static (string, bool) CheckPathExists(ValidationRule r, string value)
    {
        if (value.Trim() == "")
        {
            return ("path is empty", true);
        }
        var isFile = File.Exists(value);
        var isDir = Directory.Exists(value);
        if (!isFile && !isDir)
        {
            return ($"path does not exist: {value}", true);
        }
        switch (r.PathMode)
        {
            case PathMode.File:
                if (isDir)
                {
                    return ($"expected a file but found a directory: {value}", true);
                }
                break;
            case PathMode.Dir:
                if (!isDir)
                {
                    return ($"expected a directory but found a file: {value}", true);
                }
                break;
        }
        return ("", false);
    }

    private static (string, bool) CheckRegex(ValidationRule r, string value)
    {
        Regex re;
        try
        {
            re = new Regex(r.Pattern ?? "");
        }
        catch (ArgumentException ex)
        {
            return ($"internal error: invalid regex pattern \"{r.Pattern}\": {ex.Message}", true);
        }
        if (!re.IsMatch(value))
        {
            return ($"value \"{value}\" does not match required pattern \"{r.Pattern}\"", true);
        }
        return ("", false);
    }

    private static (string, bool) CheckEnum(ValidationRule r, string value)
    {
        if (r.Values is not null && r.Values.Contains(value))
        {
            return ("", false);
        }
        var list = "[" + string.Join(" ", r.Values ?? new List<string>()) + "]"; // matches Go's %v on []string
        return ($"value \"{value}\" is not one of the allowed values {list}", true);
    }

    private static (string, bool) CheckRange(ValidationRule r, string value)
    {
        if (!double.TryParse(value.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n))
        {
            return ($"value \"{value}\" is not numeric, cannot check range", true);
        }
        if (r.Min is { } min && n < min)
        {
            return ($"value {n} is below the minimum {min}", true);
        }
        if (r.Max is { } max && n > max)
        {
            return ($"value {n} is above the maximum {max}", true);
        }
        return ("", false);
    }
}
