namespace BravoBisConfigurator.Core.Model;

/// <summary>
///  Reports what Save actually did, including the best-effort "root copy"
///  side effect for the bravo profile: after saving the real, canonical
///  bravo.ini in the OS system directory, this tool also mirrors it next to
///  its own executable (where bis.ini lives), so an operator without access
///  to browse the system directory can still see the current content. A
///  failure there must never be reported as though the primary save (to
///  FilePath) had failed, but it must also never be silently swallowed.
///  Ported 1:1 from internal/app/save.go's SaveResult.
/// </summary>
public sealed class SaveResult
{
    /// <summary>Empty when FilePath did not exist yet — there was nothing to back up.</summary>
    public string BackupPath { get; init; } = "";

    /// <summary>
    ///  Set when the active profile is "bravo", FilePath is exactly the
    ///  canonical system-directory bravo.ini (not some other file an
    ///  operator manually browsed to via the fallback dialog), and the
    ///  copy succeeded.
    /// </summary>
    public string RootCopyPath { get; init; } = "";

    /// <summary>
    ///  Non-null when a root copy was attempted (per RootCopyPath's
    ///  conditions) but failed. The primary save to FilePath already
    ///  succeeded in that case.
    /// </summary>
    public Exception? RootCopyError { get; init; }
}
