using BravoBisConfigurator.Core.Ini;
using BravoBisConfigurator.Core.Model;
using BravoBisConfigurator.Core.Profile;
using BravoBisConfigurator.Core.Schema;
using Xunit;

namespace BravoBisConfigurator.Core.Tests;

/// <summary>
///  Covers the same cases as internal/app/model_installroot_test.go's
///  TestResolvedHint_* table, but through the public FormModel API (schema
///  with a Path field + a real Document) rather than calling the private
///  resolvedHint helper directly — Go could call the unexported function
///  in-package; C#'s equivalent (private static method) has no test-visible
///  seam without reflection, and constructing the field through FormModel
///  exercises the exact same code path a real save/load would.
/// </summary>
public class ResolvedHintTests
{
    private static BravoBisConfigurator.Core.Schema.Schema SchemaWithPathField() => new()
    {
        ProfileName = "bravo",
        Status = SchemaStatus.Verified,
        Sections = new List<SectionDef>
        {
            new()
            {
                Name = "model",
                Fields = new List<FieldDef> { new() { Key = "BLOG", Type = FieldType.Path, Required = true } },
            },
            new()
            {
                Name = "Debug",
                Fields = new List<FieldDef> { new() { Key = "FILE", Type = FieldType.Path } },
            },
        },
    };

    private static FieldView DebugFileField(Document doc)
    {
        Assert.True(ProfileDefinition.TryFind("bravo", out var prof));
        var m = new FormModel(prof, SchemaWithPathField(), doc, IniEncoding.Utf8, "bravo.ini");
        return m.Sections.First(s => s.Name == "Debug").Fields.First(f => f.Key == "FILE");
    }

    [Fact]
    public void ResolvedHint_RelativePathField_ComputesJoinedPath()
    {
        var doc = Parser.Parse("[model]\nBLOG=D:\\LIMS-NEW\\BLOG\\\n[Debug]\nFILE=TraceSRV.out\n", ParseOptions.Default());
        Assert.Equal(@"D:\LIMS-NEW\TraceSRV.out", DebugFileField(doc).ResolvedHint);
    }

    [Fact]
    public void ResolvedHint_AbsolutePathField_NoHint()
    {
        var doc = Parser.Parse("[model]\nBLOG=D:\\LIMS-NEW\\BLOG\\\n[Debug]\nFILE=D:\\LIMS-NEW\\Model\\lims\n", ParseOptions.Default());
        Assert.Equal("", DebugFileField(doc).ResolvedHint);
    }

    [Fact]
    public void ResolvedHint_UnknownRoot_NoHint()
    {
        // No absolute BLOG/BEXCH anywhere -> InstallRoot is unknown.
        var doc = Parser.Parse("[Debug]\nFILE=TraceSRV.out\n", ParseOptions.Default());
        Assert.Equal("", DebugFileField(doc).ResolvedHint);
    }

    [Fact]
    public void ResolvedHint_EmptyValue_NoHint()
    {
        var doc = Parser.Parse("[model]\nBLOG=D:\\LIMS-NEW\\BLOG\\\n", ParseOptions.Default());
        Assert.Equal("", DebugFileField(doc).ResolvedHint);
    }
}
