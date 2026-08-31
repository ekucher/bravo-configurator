using BravoBisConfigurator.Core.Ini;
using BravoBisConfigurator.Core.Model;
using BravoBisConfigurator.Core.Profile;
using Xunit;

namespace BravoBisConfigurator.Core.Tests;

/// <summary>Ported 1:1 from internal/app/save_test.go.</summary>
public class SaveTests
{
    [Fact]
    public void Save_BlockedWhileErrorsRemain()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var path = Path.Combine(dir.FullName, "bravo.ini");
            File.WriteAllText(path, "[model]\nMODEL=x\n");

            var (doc, enc) = IniFile.ReadFile(path, ParseOptions.Default());
            Assert.True(ProfileDefinition.TryFind("bravo", out var prof));
            var m = new FormModel(prof, TestSchemas.Bravo(), doc, enc, path); // BLOG required and missing

            Assert.Throws<InvalidOperationException>(() => m.Save());
            // The file on disk must be untouched.
            Assert.Equal("[model]\nMODEL=x\n", File.ReadAllText(path));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void Save_BacksUpAndWritesAtomically()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var path = Path.Combine(dir.FullName, "bravo.ini");
            var original = "[model]\nMODEL=old\nBLOG=y\n";
            File.WriteAllText(path, original);

            var (doc, enc) = IniFile.ReadFile(path, ParseOptions.Default());
            Assert.True(ProfileDefinition.TryFind("bravo", out var prof));
            var m = new FormModel(prof, TestSchemas.Bravo(), doc, enc, path);
            m.ApplyEdit("model", "MODEL", "new");

            var result = m.Save();
            Assert.NotEqual("", result.BackupPath);
            Assert.Equal("", result.RootCopyPath);
            Assert.Null(result.RootCopyError);

            Assert.Equal(original, File.ReadAllText(result.BackupPath));
            Assert.Equal("[model]\nMODEL=new\nBLOG=y\n", File.ReadAllText(path));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void Save_NewFile_NoBackupPath()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var path = Path.Combine(dir.FullName, "new-bravo.ini");
            var doc = Parser.Parse("", ParseOptions.Default());
            doc.Set("model", "MODEL", "m");
            doc.Set("model", "BLOG", "b");

            Assert.True(ProfileDefinition.TryFind("bravo", out var prof));
            var m = new FormModel(prof, TestSchemas.Bravo(), doc, IniEncoding.Utf8, path);

            var result = m.Save();
            Assert.Equal("", result.BackupPath);
            Assert.True(File.Exists(path));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>
    ///  Exercises Save's "copy bravo.ini next to the executable" side
    ///  effect by injecting systemBravoIniPathFunc/executableDirFunc
    ///  through FormModel's constructor, so it never touches the real
    ///  system directory or the test binary's own temp directory.
    /// </summary>
    [Fact]
    public void Save_RootCopy_MirrorsToExecutableDirWhenPathIsCanonical()
    {
        var systemDir = Directory.CreateTempSubdirectory();
        var rootDir = Directory.CreateTempSubdirectory();
        try
        {
            var canonicalPath = Path.Combine(systemDir.FullName, "bravo.ini");
            var original = "[model]\nMODEL=old\nBLOG=y\n";
            File.WriteAllText(canonicalPath, original);

            var (doc, enc) = IniFile.ReadFile(canonicalPath, ParseOptions.Default());
            Assert.True(ProfileDefinition.TryFind("bravo", out var prof));
            var m = new FormModel(
                prof, TestSchemas.Bravo(), doc, enc, canonicalPath,
                systemBravoIniPathFunc: () => canonicalPath,
                executableDirFunc: () => rootDir.FullName);
            m.ApplyEdit("model", "MODEL", "new");

            var result = m.Save();
            Assert.Null(result.RootCopyError);
            var wantRootPath = Path.Combine(rootDir.FullName, "bravo.ini");
            Assert.Equal(wantRootPath, result.RootCopyPath);
            Assert.Equal("[model]\nMODEL=new\nBLOG=y\n", File.ReadAllText(wantRootPath));
        }
        finally
        {
            systemDir.Delete(recursive: true);
            rootDir.Delete(recursive: true);
        }
    }

    /// <summary>
    ///  Confirms the root-copy side effect is bravo-only: bis.ini is
    ///  already read from (and saved to) the executable's own directory, so
    ///  mirroring it a second time makes no sense.
    /// </summary>
    [Fact]
    public void Save_RootCopy_SkippedForBisProfile()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var path = Path.Combine(dir.FullName, "bis.ini");
            File.WriteAllText(path, "[model]\nMODEL=x\nBLOG=y\n");

            var (doc, enc) = IniFile.ReadFile(path, ParseOptions.Default());
            Assert.True(ProfileDefinition.TryFind("bis", out var prof));
            var m = new FormModel(prof, TestSchemas.Bravo(), doc, enc, path);

            var result = m.Save();
            Assert.Equal("", result.RootCopyPath);
            Assert.Null(result.RootCopyError);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
