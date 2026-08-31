using BravoBisConfigurator.Core.Schema;
using Xunit;

namespace BravoBisConfigurator.Core.Tests.Schema;

public class LoaderTests
{
    [Theory]
    [InlineData("bravo")]
    [InlineData("bis")]
    public void LoadEmbedded_RealSchemas_ParseWithoutError(string profile)
    {
        var s = Loader.LoadEmbedded(profile);
        Assert.Equal(profile, s.ProfileName);
        Assert.NotEmpty(s.Sections);
        Assert.Contains(s.Sections, sec => sec.Fields.Count > 0);
    }

    [Fact]
    public void LoadEmbedded_Bravo_ModelSectionHasRequiredPathFields()
    {
        var s = Loader.LoadEmbedded("bravo");
        Assert.True(s.FindField("model", "MODEL", out var field, out _));
        Assert.NotNull(field);
        Assert.Equal(FieldType.Path, field!.Type);
        Assert.True(field.Required);
        Assert.NotNull(field.Validation);
        Assert.Equal(RuleKind.PathExists, field.Validation!.Kind);
        Assert.Equal(PathMode.Either, field.Validation.PathMode);
        Assert.Equal(Severity.Error, field.Validation.EffectiveSeverity());
    }

    [Fact]
    public void LoadEmbedded_Bis_EnumFieldParsesValuesAndWarningSeverity()
    {
        var s = Loader.LoadEmbedded("bis");
        Assert.True(s.FindField("config", "checkApp", out var field, out _));
        Assert.NotNull(field);
        Assert.Equal(FieldType.Enum, field!.Type);
        Assert.NotNull(field.Validation);
        Assert.Equal(RuleKind.Enum, field.Validation!.Kind);
        Assert.Equal(new List<string> { "on", "off" }, field.Validation.Values);
        Assert.Equal(Severity.Warning, field.Validation.EffectiveSeverity());
    }

    [Fact]
    public void LoadEmbedded_UnknownProfile_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Loader.LoadEmbedded("nonexistent"));
    }
}
