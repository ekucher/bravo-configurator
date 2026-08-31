using BravoBisConfigurator.Core.Locate;
using BravoBisConfigurator.Core.Profile;

namespace BravoBisConfigurator.Core.Model;

/// <summary>
///  Auto-discovery of each profile's default file path. Ported 1:1 from
///  internal/app/discover.go.
/// </summary>
public static class Discover
{
    /// <summary>
    ///  prof's auto-discovered path:
    ///
    ///  - bravo.ini: the OS system directory (bravo.exe, the LIMS server
    ///    component, is the canonical writer there; this tool is not
    ///    deployed next to the real bravo.ini).
    ///  - every other profile (bis.ini today): FileHint next to the
    ///    running configurator.exe, matching the LIMS client install
    ///    layout this tool ships inside alongside bis.ini.
    /// </summary>
    public static string DefaultPathForProfile(ProfileDefinition prof)
    {
        if (prof.Name == "bravo")
        {
            return LocateService.SystemBravoIniPath();
        }
        var dir = LocateService.ExecutableDir();
        return Path.Combine(dir, prof.FileHint);
    }
}
