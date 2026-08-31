namespace BravoBisConfigurator.Core.Backup;

/// <summary>
///  The two safety primitives every save goes through: a timestamped copy
///  of the file being overwritten, and an atomic (temp-file + rename)
///  write so a crash or failure mid-write never leaves a truncated/corrupt
///  file in the target's place. Ported 1:1 from internal/backup/backup.go
///  and internal/backup/atomic.go.
/// </summary>
public static class Atomic
{
    // Produces names like bravo.ini.20260831-143012.bak. Deliberately
    // excludes anything finer than seconds; rapid consecutive collisions
    // within the same second are resolved by UniquePath below rather than
    // by adding sub-second precision, so backup filenames stay readable.
    private const string TimestampFormat = "yyyyMMdd-HHmmss";

    /// <summary>
    ///  Copies path to "&lt;path&gt;.&lt;YYYYMMDD-HHMMSS&gt;.bak" (with a
    ///  "-N" suffix inserted before ".bak" if that name is already taken)
    ///  and returns the backup's path. If path does not exist yet (e.g.
    ///  creating a brand-new file from schema defaults), there is nothing
    ///  to back up: returns "" rather than throwing.
    ///
    ///  The backup is made by copying through a fresh read handle, not by
    ///  renaming the original — so if the copy fails partway (disk full,
    ///  I/O error), the original file at path is left completely untouched
    ///  and the caller can abort the save before any mutation is attempted.
    /// </summary>
    public static string TimestampedBackup(string path)
    {
        if (!File.Exists(path))
        {
            return "";
        }

        using var src = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        var candidate = $"{path}.{DateTime.Now.ToString(TimestampFormat)}.bak";
        candidate = UniquePath(candidate);

        // Mirrors os.O_WRONLY|O_CREATE|O_EXCL: fails if the (unique)
        // candidate somehow already exists between UniquePath's check and
        // this open.
        FileStream dst;
        try
        {
            dst = new FileStream(candidate, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        }
        catch (IOException ex)
        {
            throw new IOException($"backup: create {candidate}: {ex.Message}", ex);
        }

        // If anything below fails, remove the partial backup file rather
        // than leaving a corrupt .bak behind.
        var ok = false;
        try
        {
            src.CopyTo(dst);
            dst.Flush(flushToDisk: true);
            ok = true;
            return candidate;
        }
        catch (IOException ex)
        {
            throw new IOException($"backup: copy to {candidate}: {ex.Message}", ex);
        }
        finally
        {
            dst.Close();
            if (!ok)
            {
                File.Delete(candidate);
            }
        }
    }

    /// <summary>
    ///  Returns p unchanged if nothing exists at that path yet, otherwise
    ///  inserts "-1", "-2", ... before the ".bak" extension until an unused
    ///  name is found. Makes rapid consecutive saves (same second) produce
    ///  distinct backups instead of one overwriting the other.
    /// </summary>
    private static string UniquePath(string p)
    {
        if (!File.Exists(p))
        {
            return p;
        }
        var ext = Path.GetExtension(p);
        var basePath = p[..^ext.Length];
        for (var i = 1; ; i++)
        {
            var candidate = $"{basePath}-{i}{ext}";
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    /// <summary>
    ///  Writes data to path without ever leaving a partially-written file
    ///  in its place: writes to a temp file in the same directory
    ///  (guaranteeing same-volume rename, atomic on NTFS), flushes it,
    ///  closes it, then moves it over path. If any step before the move
    ///  fails, the temp file is removed and path is left completely
    ///  untouched. Callers that want a safety copy of the previous
    ///  contents should call TimestampedBackup(path) first.
    ///
    ///  Ported from internal/backup/atomic.go, with one deliberate .NET-vs-Go
    ///  difference: Go's os.Rename replaces an existing destination on
    ///  Windows; .NET's File.Move does not unless overwrite:true is passed
    ///  explicitly — passed here so behavior matches.
    /// </summary>
    public static void AtomicWrite(string path, byte[] data)
    {
        var dir = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(dir))
        {
            dir = ".";
        }
        var tmpPath = Path.Combine(dir, $".tmp-{Path.GetFileName(path)}-{Guid.NewGuid():N}");

        var renamed = false;
        try
        {
            using (var tmp = new FileStream(tmpPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                tmp.Write(data, 0, data.Length);
                tmp.Flush(flushToDisk: true);
            }
            File.Move(tmpPath, path, overwrite: true);
            renamed = true;
        }
        finally
        {
            if (!renamed)
            {
                File.Delete(tmpPath);
            }
        }
    }
}
