using BravoBisConfigurator.Core.Ini;
using BravoBisConfigurator.Core.Model;
using BravoBisConfigurator.Core.Profile;
using Xunit;

namespace BravoBisConfigurator.Core.Tests;

/// <summary>Ported 1:1 from internal/app/model_test.go.</summary>
public class FormModelTests
{
    [Fact]
    public void NewFormModel_FieldCountMatchesSchema()
    {
        var doc = Parser.Parse("[model]\nMODEL=x\n", ParseOptions.Default());
        Assert.True(ProfileDefinition.TryFind("bravo", out var prof));
        var m = new FormModel(prof, TestSchemas.Bravo(), doc, IniEncoding.Utf8, "bravo.ini");

        Assert.Single(m.Sections);
        Assert.Equal(2, m.Sections[0].Fields.Count);
    }

    [Fact]
    public void NewFormModel_CanSave_TogglesWithRequiredField()
    {
        var doc = Parser.Parse("[model]\nMODEL=x\n", ParseOptions.Default());
        Assert.True(ProfileDefinition.TryFind("bravo", out var prof));
        var m = new FormModel(prof, TestSchemas.Bravo(), doc, IniEncoding.Utf8, "bravo.ini");

        Assert.False(m.CanSave()); // BLOG required and missing

        var blogHasError = m.Sections[0].Fields.First(f => f.Key == "BLOG").HasError();
        Assert.True(blogHasError);

        m.ApplyEdit("model", "BLOG", "y");
        Assert.True(m.CanSave());
    }

    [Fact]
    public void ApplyEdit_UpdatesFieldValueAndUnderlyingDocument()
    {
        var doc = Parser.Parse("[model]\nMODEL=old\nBLOG=y\n", ParseOptions.Default());
        Assert.True(ProfileDefinition.TryFind("bravo", out var prof));
        var m = new FormModel(prof, TestSchemas.Bravo(), doc, IniEncoding.Utf8, "bravo.ini");

        m.ApplyEdit("model", "MODEL", "new");

        Assert.Equal("new", m.Doc.Get("model", "MODEL"));
        var field = m.Sections[0].Fields.FirstOrDefault(f => f.Key == "MODEL");
        Assert.NotNull(field);
        Assert.Equal("new", field!.Value);
    }

    [Fact]
    public void UnrecognizedFindings_OnlyIncludesUnknownKeys()
    {
        var doc = Parser.Parse("[model]\nMODEL=x\nBLOG=y\nFutureKey=z\n", ParseOptions.Default());
        Assert.True(ProfileDefinition.TryFind("bravo", out var prof));
        var m = new FormModel(prof, TestSchemas.Bravo(), doc, IniEncoding.Utf8, "bravo.ini");

        var unrec = m.UnrecognizedFindings();
        Assert.Single(unrec);
        Assert.Equal("FutureKey", unrec[0].Key);
    }
}
