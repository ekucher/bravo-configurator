namespace BravoBisConfigurator.Core.Locate;

/// <summary>
///  Resolves the two well-known on-disk locations this tool auto-discovers
///  its configuration files from, instead of requiring an operator to
///  browse for them every time:
///
///  - bravo.ini lives in the OS system directory (bravo.exe is the
///    canonical writer; see SystemBravoIniPath).
///  - bis.ini lives next to the running configurator.exe itself, in
///    whatever directory the LIMS client install placed it.
///
///  Ported from internal/locate/locate.go. One deliberate simplification
///  versus the Go version: OS-bitness detection uses the BCL's
///  Environment.Is64BitOperatingSystem directly — .NET already exposes
///  this natively, so the Go version's P/Invoke IsWow64Process fallback
///  (needed only because Go has no equivalent built-in) has no counterpart
///  here.
/// </summary>
public static class LocateService
{
    /// <summary>Overrides real-environment inputs for deterministic tests.</summary>
    public sealed class Options
    {
        /// <summary>Overrides Environment.GetEnvironmentVariable("SystemRoot") when non-null.</summary>
        public string? SystemRoot { get; init; }

        /// <summary>Overrides the real OS-bitness detection when non-null.</summary>
        public bool? Is64BitOs { get; init; }
    }

    /// <summary>
    ///  The OS system directory that owns bravo.ini: SysWOW64 on a 64-bit
    ///  OS, System32 on a 32-bit OS.
    ///
    ///  bravo.exe (the LIMS server component that owns bravo.ini) is a
    ///  32-bit process. On a 64-bit OS, a 32-bit process's accesses to
    ///  "System32" are transparently redirected by WOW64 to SysWOW64 —
    ///  SysWOW64 is therefore the one real, absolute directory bravo.ini
    ///  lives in on disk, regardless of which process (32- or 64-bit) later
    ///  reads that literal path. This exactly mirrors BRAVO-Toolkit's
    ///  Get-BRAVOSystemDirectoryPath so both tools agree on the same
    ///  authoritative location.
    /// </summary>
    public static string SystemDirectory(Options? opts = null)
    {
        opts ??= new Options();
        var systemRoot = opts.SystemRoot ?? Environment.GetEnvironmentVariable("SystemRoot");
        if (string.IsNullOrEmpty(systemRoot))
        {
            throw new InvalidOperationException("locate: %SystemRoot% is not set and no override was given");
        }

        var is64 = opts.Is64BitOs ?? Environment.Is64BitOperatingSystem;
        var sub = is64 ? "SysWOW64" : "System32";
        return Path.Combine(systemRoot, sub);
    }

    /// <summary>
    ///  The canonical, authoritative path to the server-side bravo.ini:
    ///  SystemDirectory()\bravo.ini.
    /// </summary>
    public static string SystemBravoIniPath(Options? opts = null) =>
        Path.Combine(SystemDirectory(opts), "bravo.ini");

    /// <summary>
    ///  The directory containing the running executable. Uses
    ///  AppContext.BaseDirectory (the .NET-idiomatic equivalent of Go's
    ///  os.Executable — already resolved, no symlink handling needed).
    /// </summary>
    public static string ExecutableDir() => AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
