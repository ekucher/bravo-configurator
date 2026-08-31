using BravoBisConfigurator.Core.Schema;
using BravoBisConfigurator.Core.Validate;

namespace BravoBisConfigurator.Core.Model;

/// <summary>
///  One schema field bound to its current value in Doc and any validation
///  findings that apply specifically to it. Ported 1:1 from
///  internal/app/model.go's FieldView.
/// </summary>
public sealed class FieldView
{
    public required string Section { get; init; }
    public required string Key { get; init; }
    public required string Label { get; init; }
    public string? Description { get; init; }
    public required FieldType Type { get; init; }
    public required string Value { get; init; }
    public bool Required { get; init; }
    public ValidationRule? Rule { get; init; }
    public List<Result> Findings { get; init; } = new();

    /// <summary>
    ///  The absolute path Value would resolve to against FormModel.
    ///  InstallRoot, shown to the operator as informational-only context
    ///  (e.g. a tooltip) — set only for FieldType.Path fields holding a
    ///  non-empty, relative value while InstallRoot is known. Never fed
    ///  back into Doc/ApplyEdit: the saved file always keeps the
    ///  operator's actual (possibly relative) value untouched.
    /// </summary>
    public string ResolvedHint { get; init; } = "";

    /// <summary>
    ///  Whether this field has a Severity.Error finding — the GUI's widget
    ///  factory uses this to render the field's error state.
    /// </summary>
    public bool HasError() => Findings.Any(r => r.Severity == Severity.Error);
}

/// <summary>One schema section's fields, in schema order.</summary>
public sealed class SectionView
{
    public required string Name { get; init; }
    public required string Label { get; init; }
    public string? Description { get; init; }
    public List<FieldView> Fields { get; init; } = new();
}
