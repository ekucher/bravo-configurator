namespace BravoBisConfigurator.Core.Profile;

/// <summary>
///  Identifies one of the two configuration files this tool edits. Ported
///  1:1 from internal/profile/profile.go — the small registry mapping a
///  profile name ("bravo" or "bis") to its bundled schema and a suggested
///  default filename.
/// </summary>
public sealed class ProfileDefinition
{
    /// <summary>Also the key Schema.Loader.LoadEmbedded expects.</summary>
    public required string Name { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>The filename the GUI's file-open dialog defaults to.</summary>
    public required string FileHint { get; init; }

    private static readonly List<ProfileDefinition> Registry = new()
    {
        new ProfileDefinition { Name = "bravo", DisplayName = "BRAVO (сервер)", FileHint = "bravo.ini" },
        new ProfileDefinition { Name = "bis", DisplayName = "BIS (клієнт)", FileHint = "bis.ini" },
    };

    /// <summary>Every registered profile, in a stable, fixed order.</summary>
    public static IReadOnlyList<ProfileDefinition> All() => Registry;

    /// <summary>Looks up a profile by name.</summary>
    public static bool TryFind(string name, out ProfileDefinition profile)
    {
        foreach (var p in Registry)
        {
            if (p.Name == name)
            {
                profile = p;
                return true;
            }
        }
        profile = null!;
        return false;
    }

    /// <summary>Loads this profile's bundled default schema.</summary>
    public Schema.Schema LoadSchema() => Schema.Loader.LoadEmbedded(Name);
}
