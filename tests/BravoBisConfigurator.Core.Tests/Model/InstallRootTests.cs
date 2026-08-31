using BravoBisConfigurator.Core.Ini;
using BravoBisConfigurator.Core.Model;
using BravoBisConfigurator.Core.Profile;
using Xunit;

namespace BravoBisConfigurator.Core.Tests;

/// <summary>Ported 1:1 from internal/app/model_installroot_test.go.</summary>
public class InstallRootTests
{
    [Fact]
    public void InstallRoot_Bravo_FromBlog()
    {
        var doc = Parser.Parse("[model]\nBLOG=D:\\LIMS-NEW\\BLOG\\\n", ParseOptions.Default());
        Assert.True(ProfileDefinition.TryFind("bravo", out var prof));
        var m = new FormModel(prof, TestSchemas.Bravo(), doc, IniEncoding.Utf8, "bravo.ini");
        Assert.Equal(@"D:\LIMS-NEW", m.InstallRoot);
    }

    [Fact]
    public void InstallRoot_Bravo_FallsBackToBexch()
    {
        var doc = Parser.Parse("[model]\nBEXCH=D:\\LIMS-NEW\\bravoexch\n", ParseOptions.Default());
        Assert.True(ProfileDefinition.TryFind("bravo", out var prof));
        var m = new FormModel(prof, TestSchemas.Bravo(), doc, IniEncoding.Utf8, "bravo.ini");
        Assert.Equal(@"D:\LIMS-NEW", m.InstallRoot);
    }

    [Fact]
    public void InstallRoot_Bravo_NoAbsoluteValueAnywhere_ReturnsEmpty()
    {
        var doc = Parser.Parse("[model]\nBLOG=relative\\path\n", ParseOptions.Default());
        Assert.True(ProfileDefinition.TryFind("bravo", out var prof));
        var m = new FormModel(prof, TestSchemas.Bravo(), doc, IniEncoding.Utf8, "bravo.ini");
        Assert.Equal("", m.InstallRoot);
    }

    [Fact]
    public void InstallRoot_Bis_UsesExecutableDir()
    {
        var doc = Parser.Parse("", ParseOptions.Default());
        Assert.True(ProfileDefinition.TryFind("bis", out var prof));
        var m = new FormModel(
            prof, TestSchemas.Bravo(), doc, IniEncoding.Utf8, "bis.ini",
            executableDirFunc: () => @"E:\LIMS-Client");
        Assert.Equal(@"E:\LIMS-Client", m.InstallRoot);
    }
}
