using BravoBisConfigurator.Core.Locate;
using Xunit;

namespace BravoBisConfigurator.Core.Tests;

/// <summary>Ported 1:1 from internal/locate/locate_test.go.</summary>
public class LocateTests
{
    [Fact]
    public void SystemDirectory_64BitOs_UsesSysWow64()
    {
        var got = LocateService.SystemDirectory(new LocateService.Options { SystemRoot = @"C:\Windows", Is64BitOs = true });
        var want = Path.Combine(@"C:\Windows", "SysWOW64");
        Assert.Equal(want, got);
    }

    [Fact]
    public void SystemDirectory_32BitOs_UsesSystem32()
    {
        var got = LocateService.SystemDirectory(new LocateService.Options { SystemRoot = @"C:\Windows", Is64BitOs = false });
        var want = Path.Combine(@"C:\Windows", "System32");
        Assert.Equal(want, got);
    }

    [Fact]
    public void SystemDirectory_MissingSystemRoot_Throws()
    {
        var original = Environment.GetEnvironmentVariable("SystemRoot");
        try
        {
            Environment.SetEnvironmentVariable("SystemRoot", "");
            Assert.Throws<InvalidOperationException>(() => LocateService.SystemDirectory(new LocateService.Options { Is64BitOs = true }));
        }
        finally
        {
            Environment.SetEnvironmentVariable("SystemRoot", original);
        }
    }

    [Fact]
    public void SystemDirectory_RealEnvironment_NoOverride()
    {
        // Sanity check against the real environment this test runs in (a
        // real Windows machine): just confirm it resolves without error and
        // picks one of the two known subdirectory names.
        var got = LocateService.SystemDirectory();
        var baseName = Path.GetFileName(got);
        Assert.True(baseName is "SysWOW64" or "System32", $"unexpected system subdirectory: {got}");
    }

    [Fact]
    public void SystemBravoIniPath_JoinsFileName()
    {
        var got = LocateService.SystemBravoIniPath(new LocateService.Options { SystemRoot = @"C:\Windows", Is64BitOs = true });
        var want = Path.Combine(@"C:\Windows", "SysWOW64", "bravo.ini");
        Assert.Equal(want, got);
    }

    [Fact]
    public void ExecutableDir_ResolvesToAnExistingDirectory()
    {
        var dir = LocateService.ExecutableDir();
        Assert.True(Directory.Exists(dir), $"ExecutableDir() = {dir} does not exist");
    }
}
