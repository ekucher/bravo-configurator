namespace BravoBisConfigurator.Core.Ini;

/// <summary>
///  Controls how Document.Get/Set resolve a key that appears more than once
///  within the same logical section (including across repeated "[Section]"
///  headers in the same file). Ported 1:1 from internal/ini/options.go.
/// </summary>
public enum DuplicateKeyPolicy
{
    /// <summary>
    ///  Mirrors BRAVO-Toolkit's confirmed bravo.ini reader
    ///  (ConvertFrom-BRAVOIniFile): the last occurrence of a duplicated key
    ///  wins. This is the default.
    /// </summary>
    LastWins,

    /// <summary>Resolves duplicates to the first occurrence instead.</summary>
    FirstWins,

    /// <summary>
    ///  Makes Parse fail with a DuplicateKeyException as soon as a key
    ///  repeats within the same logical section, instead of silently
    ///  picking a winner.
    /// </summary>
    ErrorOnDuplicate,
}

/// <summary>
///  Controls Parser.Parse's syntax handling. Use <see cref="Default"/> to
///  get BRAVO-Toolkit-compatible defaults and override individual
///  properties from there.
/// </summary>
public sealed class ParseOptions
{
    /// <summary>
    ///  The characters that start a comment when they are the first
    ///  non-blank character on a line. Confirmed production behavior
    ///  (BRAVO.Discovery.psm1) uses ';' only; '#' is offered here as a
    ///  defensive parser capability, not because it has been observed in a
    ///  real bravo.ini/bis.ini file.
    /// </summary>
    public char[] CommentPrefixes { get; set; } = { ';' };

    public DuplicateKeyPolicy DuplicateKeyPolicy { get; set; } = DuplicateKeyPolicy.LastWins;

    /// <summary>
    ///  Whether section names and keys are compared case-insensitively for
    ///  Get/Set/duplicate detection. Confirmed production behavior is
    ///  case-insensitive.
    /// </summary>
    public bool CaseInsensitiveKeys { get; set; } = true;

    /// <summary>
    ///  Options matching the confirmed behavior of BRAVO-Toolkit's
    ///  ConvertFrom-BRAVOIniFile: ';'-only comments, last-duplicate-wins,
    ///  case-insensitive section/key matching.
    /// </summary>
    public static ParseOptions Default() => new();
}
