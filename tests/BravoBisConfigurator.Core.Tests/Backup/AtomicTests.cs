using BravoBisConfigurator.Core.Backup;
using Xunit;

namespace BravoBisConfigurator.Core.Tests.Backup;

/// <summary>Ported 1:1 from internal/backup/atomic_test.go.</summary>
public class AtomicTests
{
    [Fact]
    public void AtomicWrite_CreatesNewFile()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var path = Path.Combine(dir.FullName, "bravo.ini");
            var content = "[model]\nMODEL=x\n"u8.ToArray();

            Atomic.AtomicWrite(path, content);
            Assert.Equal(content, File.ReadAllBytes(path));
            AssertNoTempFilesLeftBehind(dir.FullName);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void AtomicWrite_ReplacesExistingFile()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var path = Path.Combine(dir.FullName, "bravo.ini");
            File.WriteAllText(path, "old");
            var newContent = "[model]\nMODEL=new\n"u8.ToArray();

            Atomic.AtomicWrite(path, newContent);
            Assert.Equal(newContent, File.ReadAllBytes(path));
            AssertNoTempFilesLeftBehind(dir.FullName);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void AtomicWrite_FailedMoveLeavesOriginalUntouched()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var original = "original-content"u8.ToArray();
            // Use a directory as the destination "path" so the final
            // File.Move step is guaranteed to fail (can't move a regular
            // temp file onto an existing directory), letting us verify the
            // pre-move content is left exactly as it was and no partial
            // file appears in its place.
            var targetDir = Path.Combine(dir.FullName, "bravo.ini"); // name reused as a directory below
            Directory.CreateDirectory(targetDir);
            var marker = Path.Combine(targetDir, "marker.txt");
            File.WriteAllBytes(marker, original);

            // On Windows, moving a file onto an existing directory raises
            // UnauthorizedAccessException, not IOException — either way, the
            // move must fail rather than silently succeed or corrupt data.
            Assert.ThrowsAny<Exception>(() => Atomic.AtomicWrite(targetDir, "new-content"u8.ToArray()));

            // The directory (and the marker file proving it wasn't
            // replaced) must still be exactly as before.
            Assert.Equal(original, File.ReadAllBytes(marker));
            AssertNoTempFilesLeftBehind(dir.FullName);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    private static void AssertNoTempFilesLeftBehind(string dir)
    {
        foreach (var entry in Directory.GetFileSystemEntries(dir))
        {
            var name = Path.GetFileName(entry);
            Assert.False(name.StartsWith(".tmp-"), $"stray temp file left behind: {name}");
        }
    }
}
