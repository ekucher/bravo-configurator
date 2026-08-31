using BravoBisConfigurator.Core.Backup;
using BravoBisConfigurator.Core.Ini;

namespace BravoBisConfigurator.Core.Model;

/// <summary>
///  Save/root-copy behavior, split into its own partial-class file the same
///  way internal/app/save.go is a separate file from model.go.
/// </summary>
public sealed partial class FormModel
{
    /// <summary>
    ///  Backs up FilePath (if it already exists) then atomically writes the
    ///  current in-memory document to it, re-encoded with Encoding — the
    ///  same encoding the file was originally read with, so a save never
    ///  silently changes the codepage the external LIMS/BIS application
    ///  expects.
    ///
    ///  Refuses to run while the model has any Severity.Error finding,
    ///  mirroring the GUI's disabled Save button, so the guard holds even
    ///  if a caller reaches this method some other way than clicking Save.
    /// </summary>
    public SaveResult Save()
    {
        if (!CanSave())
        {
            throw new InvalidOperationException("app: refusing to save while validation errors remain");
        }

        string backupPath;
        try
        {
            backupPath = Atomic.TimestampedBackup(FilePath);
        }
        catch (Exception ex)
        {
            throw new IOException($"app: backup failed, original left untouched: {ex.Message}", ex);
        }

        byte[] data;
        try
        {
            data = IniFile.RenderFile(Doc, Encoding);
        }
        catch (Exception ex)
        {
            throw new IOException($"app: encode failed: {ex.Message}", ex);
        }

        try
        {
            Atomic.AtomicWrite(FilePath, data);
        }
        catch (Exception ex)
        {
            throw new IOException($"app: write failed: {ex.Message}", ex);
        }

        var result = new SaveResult { BackupPath = backupPath };
        if (TryRootCopyTarget(out var rootPath))
        {
            try
            {
                File.WriteAllBytes(rootPath, data);
                return new SaveResult { BackupPath = backupPath, RootCopyPath = rootPath };
            }
            catch (Exception ex)
            {
                return new SaveResult
                {
                    BackupPath = backupPath,
                    RootCopyError = new IOException($"app: copying to {rootPath}: {ex.Message}", ex),
                };
            }
        }
        return result;
    }

    /// <summary>
    ///  Whether FilePath is exactly the canonical system-directory
    ///  bravo.ini and, if so, the path next to the running executable it
    ///  should be mirrored to after a successful save.
    /// </summary>
    private bool TryRootCopyTarget(out string path)
    {
        path = "";
        if (Profile.Name != "bravo")
        {
            return false;
        }
        string systemPath;
        try
        {
            systemPath = SystemBravoIniPathFunc();
        }
        catch
        {
            return false;
        }
        if (!SamePath(systemPath, FilePath))
        {
            return false;
        }
        string dir;
        try
        {
            dir = ExecutableDirFunc();
        }
        catch
        {
            return false;
        }
        path = Path.Combine(dir, "bravo.ini");
        return true;
    }

    /// <summary>
    ///  Compares two paths the way Windows does: case-insensitively, after
    ///  normalizing separators/"."/".." segments.
    /// </summary>
    private static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
}
