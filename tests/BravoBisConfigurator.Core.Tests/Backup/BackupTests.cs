using BravoBisConfigurator.Core.Backup;
using Xunit;

namespace BravoBisConfigurator.Core.Tests.Backup;

/// <summary>Ported 1:1 from internal/backup/backup_test.go.</summary>
public class BackupTests
{
    [Fact]
    public void TimestampedBackup_CreatesCopyWithExpectedNaming()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var path = Path.Combine(dir.FullName, "bravo.ini");
            var content = "[model]\nMODEL=x\n"u8.ToArray();
            File.WriteAllBytes(path, content);

            var backupPath = Atomic.TimestampedBackup(path);
            Assert.NotEqual("", backupPath);
            Assert.Equal(dir.FullName, Path.GetDirectoryName(backupPath));
            Assert.Equal(content, File.ReadAllBytes(backupPath));
            // Original must be untouched.
            Assert.Equal(content, File.ReadAllBytes(path));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void TimestampedBackup_NonexistentSource_NoErrorNoBackup()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var path = Path.Combine(dir.FullName, "does-not-exist.ini");
            var backupPath = Atomic.TimestampedBackup(path);
            Assert.Equal("", backupPath);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void TimestampedBackup_RapidConsecutiveCallsGetUniqueNames()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var path = Path.Combine(dir.FullName, "bravo.ini");
            File.WriteAllText(path, "v1");

            var first = Atomic.TimestampedBackup(path);
            var second = Atomic.TimestampedBackup(path);
            Assert.NotEqual(first, second);
            Assert.True(File.Exists(first));
            Assert.True(File.Exists(second));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
