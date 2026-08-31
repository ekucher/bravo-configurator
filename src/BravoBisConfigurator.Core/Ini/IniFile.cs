namespace BravoBisConfigurator.Core.Ini;

/// <summary>
///  Whole-file read/render helpers tying Parser/Writer to IniEncodingCodec.
///  Ported 1:1 from internal/ini/file.go.
/// </summary>
public static class IniFile
{
    /// <summary>
    ///  Reads path, auto-detects (or applies forceEncoding, if given) its
    ///  text encoding, and parses it with opts.
    /// </summary>
    public static (Document doc, IniEncoding enc) ReadFile(string path, ParseOptions opts, IniEncoding? forceEncoding = null)
    {
        var raw = File.ReadAllBytes(path);
        var (text, enc) = IniEncodingCodec.DetectAndDecode(raw, forceEncoding);
        var doc = Parser.Parse(text, opts);
        return (doc, enc);
    }

    /// <summary>
    ///  Serializes doc and re-encodes it as enc, returning the raw bytes
    ///  ready to be written to disk. Does not write the file itself —
    ///  callers should route the result through Backup.AtomicWrite so saves
    ///  are backed up and atomic.
    /// </summary>
    public static byte[] RenderFile(Document doc, IniEncoding enc)
    {
        var text = Writer.Write(doc);
        return IniEncodingCodec.EncodeAs(text, enc);
    }
}
