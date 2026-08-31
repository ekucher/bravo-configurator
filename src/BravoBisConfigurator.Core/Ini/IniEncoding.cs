using System.Text;

namespace BravoBisConfigurator.Core.Ini;

/// <summary>
///  Identifies the byte-level text encoding a document was read with, so it
///  can be written back with the same encoding by default instead of
///  silently upgrading a legacy file's codepage underneath the external
///  LIMS/BIS application that actually consumes it. Ported 1:1 from
///  internal/ini/encoding.go (named IniEncoding, not Encoding, to avoid
///  colliding with System.Text.Encoding).
/// </summary>
public enum IniEncoding
{
    /// <summary>
    ///  UTF-8 without a byte-order mark. The default assumption: BRAVO-
    ///  Toolkit's confirmed bravo.ini reader (ConvertFrom-BRAVOIniFile)
    ///  reads via `Get-Content -Encoding UTF8`, so production files are
    ///  UTF-8 in that deployment.
    /// </summary>
    Utf8,

    /// <summary>UTF-8 with a leading EF BB BF byte-order mark.</summary>
    Utf8Bom,

    /// <summary>
    ///  Windows-1251 (Cyrillic), offered as a manual override for legacy
    ///  files — not a verified production fact.
    /// </summary>
    Cp1251,

    /// <summary>
    ///  Windows-1252 (Western European), offered as a manual override for
    ///  legacy files.
    /// </summary>
    Cp1252,
}

/// <summary>
///  Detects and decodes raw INI file bytes, and re-encodes edited text back
///  to bytes in the same encoding. Ported 1:1 from internal/ini/encoding.go.
/// </summary>
public static class IniEncodingCodec
{
    static IniEncodingCodec()
    {
        // Windows-1251/1252 are not available by default on .NET Core/5+;
        // this registers them. Must run before any GetEncoding(125x) call —
        // the static constructor guarantees that for every call through
        // this class.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private static readonly byte[] Utf8BomBytes = { 0xEF, 0xBB, 0xBF };

    /// <summary>
    ///  Inspects raw file bytes and returns the decoded text plus the
    ///  IniEncoding it used, so the caller can write the same encoding back
    ///  on save. If forceEncoding is non-null it is used as-is (manual
    ///  override) instead of auto-detection.
    ///
    ///  Auto-detection order: UTF-8 BOM -> plain UTF-8 (this also covers
    ///  ASCII-only files, which are valid UTF-8) -> Windows-1251 as a
    ///  defensive fallback for legacy non-UTF-8 files. The CP1251 fallback
    ///  is a guess for old Windows deployments, not a verified fact.
    /// </summary>
    public static (string text, IniEncoding detected) DetectAndDecode(byte[] raw, IniEncoding? forceEncoding = null)
    {
        if (forceEncoding is { } forced)
        {
            return (DecodeAs(raw, forced), forced);
        }
        if (raw.Length >= 3 && raw[0] == Utf8BomBytes[0] && raw[1] == Utf8BomBytes[1] && raw[2] == Utf8BomBytes[2])
        {
            return (DecodeAs(raw, IniEncoding.Utf8Bom), IniEncoding.Utf8Bom);
        }
        if (IsValidUtf8(raw))
        {
            return (Encoding.UTF8.GetString(raw), IniEncoding.Utf8);
        }
        return (DecodeAs(raw, IniEncoding.Cp1251), IniEncoding.Cp1251);
    }

    private static string DecodeAs(byte[] raw, IniEncoding enc) => enc switch
    {
        IniEncoding.Utf8 => IsValidUtf8(raw)
            ? Encoding.UTF8.GetString(raw)
            : throw new FormatException("ini: content is not valid UTF-8"),
        IniEncoding.Utf8Bom => DecodeUtf8Bom(raw),
        IniEncoding.Cp1251 => Encoding.GetEncoding(1251).GetString(raw),
        IniEncoding.Cp1252 => Encoding.GetEncoding(1252).GetString(raw),
        _ => throw new ArgumentOutOfRangeException(nameof(enc), enc, "ini: unsupported encoding"),
    };

    private static string DecodeUtf8Bom(byte[] raw)
    {
        var body = raw.AsSpan(raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF ? 3 : 0);
        if (!IsValidUtf8(body))
        {
            throw new FormatException("ini: content is not valid UTF-8 (after BOM)");
        }
        return Encoding.UTF8.GetString(body);
    }

    /// <summary>Re-encodes text (decoded document text) back to raw bytes for enc.</summary>
    public static byte[] EncodeAs(string text, IniEncoding enc) => enc switch
    {
        IniEncoding.Utf8 => Encoding.UTF8.GetBytes(text),
        IniEncoding.Utf8Bom => Utf8BomBytes.Concat(Encoding.UTF8.GetBytes(text)).ToArray(),
        IniEncoding.Cp1251 => Encoding.GetEncoding(1251).GetBytes(text),
        IniEncoding.Cp1252 => Encoding.GetEncoding(1252).GetBytes(text),
        _ => throw new ArgumentOutOfRangeException(nameof(enc), enc, "ini: unsupported encoding"),
    };

    private static bool IsValidUtf8(ReadOnlySpan<byte> raw)
    {
        try
        {
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(raw);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}
