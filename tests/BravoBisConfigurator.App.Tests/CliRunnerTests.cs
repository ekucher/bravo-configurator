using BravoBisConfigurator.App;
using Xunit;

namespace BravoBisConfigurator.App.Tests;

/// <summary>
///  Ported 1:1 from cmd/configurator/main_test.go. Note: there is no
///  automated test for Run(Array.Empty&lt;string&gt;(), ...) (the no-flags
///  path), because that path calls GuiRunner.Run(), which creates a real
///  Win32 window and blocks on a message loop — it requires an interactive
///  Windows desktop session and cannot run headlessly in `dotnet test`. See
///  the manual GUI checklist in docs/BUILDING.md instead.
/// </summary>
public class CliRunnerTests
{
    [Fact]
    public void Run_ValidateKnownGoodFile_ExitsZero()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var path = Path.Combine(dir.FullName, "bravo.ini");
            // Only the required, verified fields; everything else optional.
            var content = $"[model]\nMODEL={dir.FullName}\nBLOG={dir.FullName}\nBEXCH={dir.FullName}\n";
            File.WriteAllText(path, content);

            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var code = CliRunner.Run(new[] { "--validate", "--profile", "bravo", "--file", path }, stdout, stderr);
            Assert.Equal(0, code);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void Run_ValidateMissingRequiredField_ExitsNonZero()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var path = Path.Combine(dir.FullName, "bravo.ini");
            File.WriteAllText(path, $"[model]\nMODEL={dir.FullName}\n");

            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var code = CliRunner.Run(new[] { "--validate", "--profile", "bravo", "--file", path }, stdout, stderr);
            Assert.Equal(1, code);
            Assert.Contains("BLOG", stdout.ToString());
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void Run_UnknownProfile_ExitsUsageError()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = CliRunner.Run(new[] { "--validate", "--profile", "nope", "--file", "x" }, stdout, stderr);
        Assert.Equal(2, code);
    }

    [Fact]
    public void Run_ValidateWithCustomSchemaOverride()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var iniPath = Path.Combine(dir.FullName, "custom.ini");
            File.WriteAllText(iniPath, "[x]\nY=hello\n");

            var schemaPath = Path.Combine(dir.FullName, "custom.schema.yaml");
            var schemaDoc = "\n" +
                "profile: bravo\n" +
                "status: verified\n" +
                "sections:\n" +
                "  - name: x\n" +
                "    fields:\n" +
                "      - key: Y\n" +
                "        type: string\n" +
                "        required: true\n";
            File.WriteAllText(schemaPath, schemaDoc);

            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var code = CliRunner.Run(new[] { "--validate", "--profile", "bravo", "--file", iniPath, "--schema", schemaPath }, stdout, stderr);
            Assert.Equal(0, code);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void Run_ValidateMissingFileArgs_UsageError()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = CliRunner.Run(new[] { "--validate", "--profile", "bravo" }, stdout, stderr);
        Assert.Equal(2, code);
    }

    [Fact]
    public void Run_ValidateNonexistentFile_UsageError()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = CliRunner.Run(new[] { "--validate", "--profile", "bravo", "--file", "does-not-exist.ini" }, stdout, stderr);
        Assert.Equal(2, code);
    }
}
