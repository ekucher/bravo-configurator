using System.Text;
using BravoBisConfigurator.Core.Ini;
using Xunit;

namespace BravoBisConfigurator.Core.Tests.Ini;

/// <summary>Ported 1:1 from internal/ini/encoding_test.go.</summary>
public class EncodingTests
{
    static EncodingTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [Fact]
    public void DetectAndDecode_Utf8NoBom()
    {
        var raw = Encoding.UTF8.GetBytes("[model]\nMODEL=C:\\LIMS\\MODEL\n");
        var (text, enc) = IniEncodingCodec.DetectAndDecode(raw);
        Assert.Equal(IniEncoding.Utf8, enc);
        Assert.Equal(Encoding.UTF8.GetString(raw), text);
    }

    [Fact]
    public void DetectAndDecode_Utf8Bom()
    {
        var raw = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes("[model]\nMODEL=x\n")).ToArray();
        var (text, enc) = IniEncodingCodec.DetectAndDecode(raw);
        Assert.Equal(IniEncoding.Utf8Bom, enc);
        Assert.Equal("[model]\nMODEL=x\n", text);

        // Must round-trip back to byte-identical output including the BOM.
        var outBytes = IniEncodingCodec.EncodeAs(text, enc);
        Assert.Equal(raw, outBytes);
    }

    [Fact]
    public void DetectAndDecode_LegacyCp1251Fallback()
    {
        // A value containing Cyrillic text, encoded as Windows-1251 (not
        // valid UTF-8), must be auto-detected and decoded correctly since it
        // isn't valid UTF-8.
        var original = "[model]\nLABEL=Значення\n";
        var cp1251Bytes = Encoding.GetEncoding(1251).GetBytes(original);
        Assert.False(IsValidUtf8(cp1251Bytes)); // test fixture sanity check

        var (text, enc) = IniEncodingCodec.DetectAndDecode(cp1251Bytes);
        Assert.Equal(IniEncoding.Cp1251, enc);
        Assert.Equal(original, text);

        var outBytes = IniEncodingCodec.EncodeAs(text, enc);
        Assert.Equal(cp1251Bytes, outBytes);
    }

    [Fact]
    public void DetectAndDecode_ForceEncodingOverride()
    {
        var raw = Encoding.GetEncoding(1252).GetBytes("[model]\nLABEL=caf\u00e9\n");
        var (text, enc) = IniEncodingCodec.DetectAndDecode(raw, IniEncoding.Cp1252);
        Assert.Equal(IniEncoding.Cp1252, enc);
        Assert.Equal("[model]\nLABEL=caf\u00e9\n", text);
    }

    [Fact]
    public void ReadFile_RoundTripsSameEncoding()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var path = Path.Combine(dir.FullName, "bravo.ini");
            var original = new byte[] { 0xEF, 0xBB, 0xBF }
                .Concat(Encoding.UTF8.GetBytes("[model]\r\nMODEL=C:\\LIMS\\MODEL\r\n"))
                .ToArray();
            File.WriteAllBytes(path, original);

            var (doc, enc) = IniFile.ReadFile(path, ParseOptions.Default());
            Assert.Equal(IniEncoding.Utf8Bom, enc);

            var outBytes = IniFile.RenderFile(doc, enc);
            Assert.Equal(original, outBytes);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    private static bool IsValidUtf8(byte[] raw)
    {
        try
        {
            new UTF8Encoding(false, true).GetString(raw);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}
