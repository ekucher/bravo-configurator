using BravoBisConfigurator.Core.Ini;
using Xunit;

namespace BravoBisConfigurator.Core.Tests.Ini;

/// <summary>
///  Ported 1:1 from internal/ini/parser_test.go. RoundTrip is the single
///  most important property of this package: parsing a file and writing it
///  back out without touching anything must reproduce the source
///  byte-for-byte.
/// </summary>
public class ParserTests
{
    private static Document RoundTrip(string content, ParseOptions opts)
    {
        var doc = Parser.Parse(content, opts);
        var got = Writer.Write(doc);
        Assert.Equal(content, got);
        return doc;
    }

    [Fact]
    public void RoundTrip_BravoShapedSample_LF()
    {
        var content = "; bravo.ini sample\n" +
            "[system]\n" +
            "CustomKey=value\n" +
            "\n" +
            "[model]\n" +
            "MODEL=C:\\LIMS\\MODEL\n" +
            "BLOG=C:\\LIMS\\BLOG\n" +
            "BEXCH=C:\\LIMS\\BRAVOEXCH\\\n" +
            "\n" +
            "[Debug]\n" +
            "FILE=Trace\\TraceSRV.out ; relative to install dir\n";
        var doc = RoundTrip(content, ParseOptions.Default());

        Assert.True(doc.TryGet("model", "MODEL", out var v1));
        Assert.Equal(@"C:\LIMS\MODEL", v1);

        // Case-insensitive section/key lookup, per confirmed production behavior.
        Assert.True(doc.TryGet("MODEL", "model", out var v2));
        Assert.Equal(@"C:\LIMS\MODEL", v2);

        Assert.True(doc.TryGet("Debug", "FILE", out var v3));
        Assert.Equal(@"Trace\TraceSRV.out", v3); // inline comment should be split off
    }

    [Fact]
    public void RoundTrip_CRLF()
    {
        var content = "[model]\r\nMODEL=C:\\LIMS\\MODEL\r\nBLOG=C:\\LIMS\\BLOG\r\n";
        RoundTrip(content, ParseOptions.Default());
    }

    [Fact]
    public void RoundTrip_NoTrailingNewline()
    {
        var content = "[model]\nMODEL=C:\\LIMS\\MODEL";
        var doc = RoundTrip(content, ParseOptions.Default());
        Assert.False(doc.TrailingNewline);
    }

    [Fact]
    public void RoundTrip_EmptyFile()
    {
        RoundTrip("", ParseOptions.Default());
    }

    [Fact]
    public void RoundTrip_OnlyBlankLinesAndComments()
    {
        var content = "\n; leading comment\n\n; another\n";
        RoundTrip(content, ParseOptions.Default());
    }

    [Fact]
    public void RoundTrip_MalformedLinesPassThroughVerbatim()
    {
        var content = "[model]\nMODEL=C:\\LIMS\\MODEL\nthis line has no separator\n=novalue key is empty too\n";
        var doc = RoundTrip(content, ParseOptions.Default());

        var sec = doc.Sections[1];
        var rawCount = sec.Entries.Count(e => e.Kind == EntryKind.Raw);
        Assert.Equal(2, rawCount);
    }

    [Fact]
    public void DuplicateKeys_LastWins()
    {
        var content = "[model]\nMODEL=first\nMODEL=second\n";
        var doc = RoundTrip(content, ParseOptions.Default()); // LastWins is the default
        Assert.Equal("second", doc.Get("model", "MODEL"));
    }

    [Fact]
    public void DuplicateKeys_FirstWins()
    {
        var content = "[model]\nMODEL=first\nMODEL=second\n";
        var opts = ParseOptions.Default();
        opts.DuplicateKeyPolicy = DuplicateKeyPolicy.FirstWins;
        var doc = RoundTrip(content, opts);
        Assert.Equal("first", doc.Get("model", "MODEL"));
    }

    [Fact]
    public void DuplicateKeys_ErrorOnDuplicate()
    {
        var content = "[model]\nMODEL=first\nMODEL=second\n";
        var opts = ParseOptions.Default();
        opts.DuplicateKeyPolicy = DuplicateKeyPolicy.ErrorOnDuplicate;
        var ex = Assert.Throws<DuplicateKeyException>(() => Parser.Parse(content, opts));
        Assert.Equal("MODEL", ex.Key);
        Assert.Equal("model", ex.Section);
        Assert.Equal(3, ex.Line);
    }

    [Fact]
    public void DuplicateKeys_ErrorOnDuplicate_AcrossRepeatedSectionBlocks()
    {
        // Same section name reopened later in the file must still be treated
        // as one logical section for duplicate detection, matching the
        // confirmed reader's single-hashtable-per-name merge semantics.
        var content = "[model]\nMODEL=first\n[other]\nX=1\n[model]\nMODEL=second\n";
        var opts = ParseOptions.Default();
        opts.DuplicateKeyPolicy = DuplicateKeyPolicy.ErrorOnDuplicate;
        Assert.Throws<DuplicateKeyException>(() => Parser.Parse(content, opts));
    }

    [Fact]
    public void Get_UnknownSectionOrKey()
    {
        var doc = Parser.Parse("[model]\nMODEL=x\n", ParseOptions.Default());
        Assert.False(doc.TryGet("missing", "MODEL", out _));
        Assert.False(doc.TryGet("model", "missing", out _));
    }

    [Fact]
    public void Set_UpdatesInPlace_PreservesRestOfFile()
    {
        var content = "; header comment\n[model]\nMODEL=C:\\old\nBLOG=C:\\LIMS\\BLOG ; keep this comment\n";
        var doc = Parser.Parse(content, ParseOptions.Default());
        doc.Set("model", "MODEL", @"C:\new");
        var got = Writer.Write(doc);

        var want = "; header comment\n[model]\nMODEL=C:\\new\nBLOG=C:\\LIMS\\BLOG ; keep this comment\n";
        Assert.Equal(want, got);
    }

    [Fact]
    public void Set_UnknownKeyRoundTripsUnchangedWhenNotEdited()
    {
        // A field the schema doesn't know about must survive edit+save of a
        // different field, untouched.
        var content = "[model]\nMODEL=C:\\LIMS\\MODEL\nSomeFutureKey=keep-me-exactly\n";
        var doc = Parser.Parse(content, ParseOptions.Default());
        doc.Set("model", "MODEL", @"C:\LIMS\MODEL2");
        var got = Writer.Write(doc);
        Assert.Contains("SomeFutureKey=keep-me-exactly\n", got);
    }

    [Fact]
    public void Set_AppendsNewKeyToExistingSection()
    {
        var doc = Parser.Parse("[model]\nMODEL=C:\\LIMS\\MODEL\n", ParseOptions.Default());
        doc.Set("model", "BLOG", @"C:\LIMS\BLOG");
        var got = Writer.Write(doc);
        var want = "[model]\nMODEL=C:\\LIMS\\MODEL\nBLOG=C:\\LIMS\\BLOG\n";
        Assert.Equal(want, got);
    }

    [Fact]
    public void Set_CreatesNewSectionWhenAbsent()
    {
        var doc = Parser.Parse("[model]\nMODEL=x\n", ParseOptions.Default());
        doc.Set("Debug", "FILE", @"Trace\TraceSRV.out");
        var got = Writer.Write(doc);
        var want = "[model]\nMODEL=x\n[Debug]\nFILE=Trace\\TraceSRV.out\n";
        Assert.Equal(want, got);
    }
}
