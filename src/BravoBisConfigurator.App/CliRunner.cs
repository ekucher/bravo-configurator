using BravoBisConfigurator.Core.Ini;
using BravoBisConfigurator.Core.Profile;
using BravoBisConfigurator.Core.Schema;
using BravoBisConfigurator.Core.Validate;

namespace BravoBisConfigurator.App;

/// <summary>
///  The whole CLI/dispatch surface, ported 1:1 from cmd/configurator/main.go's
///  run()/runValidate(). Factored out of Program.Main so it can be
///  exercised by tests without touching real stdout/Environment.Exit or
///  spinning up the GUI.
/// </summary>
internal static class CliRunner
{
    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        var validateMode = false;
        string? profileName = null;
        string? filePath = null;
        string? schemaPath = null;
        string? encodingFlag = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--validate":
                    validateMode = true;
                    break;
                case "--profile":
                    if (!TryNextArg(args, ref i, out profileName)) return UsageError(stderr, "--profile");
                    break;
                case "--file":
                    if (!TryNextArg(args, ref i, out filePath)) return UsageError(stderr, "--file");
                    break;
                case "--schema":
                    if (!TryNextArg(args, ref i, out schemaPath)) return UsageError(stderr, "--schema");
                    break;
                case "--encoding":
                    if (!TryNextArg(args, ref i, out encodingFlag)) return UsageError(stderr, "--encoding");
                    break;
                default:
                    stderr.WriteLine($"unknown flag: {args[i]}");
                    return 2;
            }
        }

        if (!validateMode)
        {
            ApplicationConfiguration.Initialize();
            GuiRunner.Run();
            return 0;
        }

        IniEncoding? forceEncoding;
        try
        {
            forceEncoding = ParseEncoding(encodingFlag);
        }
        catch (ArgumentException ex)
        {
            stderr.WriteLine(ex.Message);
            return 2;
        }

        return RunValidate(profileName, filePath, schemaPath, forceEncoding, stdout, stderr);
    }

    private static int UsageError(TextWriter stderr, string flag)
    {
        stderr.WriteLine($"{flag} requires a value");
        return 2;
    }

    private static bool TryNextArg(string[] args, ref int i, out string value)
    {
        if (i + 1 >= args.Length)
        {
            value = "";
            return false;
        }
        value = args[++i];
        return true;
    }

    private static IniEncoding? ParseEncoding(string? flag) => flag switch
    {
        null or "" => null,
        "utf-8" => IniEncoding.Utf8,
        "utf-8-bom" => IniEncoding.Utf8Bom,
        "windows-1251" => IniEncoding.Cp1251,
        "windows-1252" => IniEncoding.Cp1252,
        _ => throw new ArgumentException($"unsupported --encoding value: \"{flag}\""),
    };

    private static string SeverityLabel(Severity s) => s == Severity.Error ? "error" : "warning";

    /// <summary>
    ///  Loads the requested (or custom) schema, parses filePath, and prints
    ///  every finding. Exits non-zero only when at least one Severity.Error
    ///  finding is present, so DRAFT-schema warnings never fail a scripted
    ///  check on their own.
    /// </summary>
    private static int RunValidate(string? profileName, string? filePath, string? schemaPath, IniEncoding? forceEncoding, TextWriter stdout, TextWriter stderr)
    {
        if (string.IsNullOrEmpty(profileName) || string.IsNullOrEmpty(filePath))
        {
            stderr.WriteLine("--validate requires --profile and --file");
            return 2;
        }
        if (!ProfileDefinition.TryFind(profileName, out var prof))
        {
            stderr.WriteLine($"unknown profile \"{profileName}\" (expected \"bravo\" or \"bis\")");
            return 2;
        }

        Core.Schema.Schema s;
        try
        {
            s = !string.IsNullOrEmpty(schemaPath) ? Loader.Load(schemaPath) : prof.LoadSchema();
        }
        catch (Exception ex)
        {
            stderr.WriteLine($"schema error: {ex.Message}");
            return 2;
        }

        Document doc;
        try
        {
            (doc, _) = IniFile.ReadFile(filePath, ParseOptions.Default(), forceEncoding);
        }
        catch (Exception ex)
        {
            stderr.WriteLine($"read error: {ex.Message}");
            return 2;
        }

        if (s.Status == SchemaStatus.Draft)
        {
            stdout.WriteLine($"NOTE: the \"{prof.Name}\" schema is DRAFT/unverified; results below may be incomplete. See docs/SCHEMA_STATUS.md.");
        }

        var results = Engine.Validate(doc, s);
        if (results.Count == 0)
        {
            stdout.WriteLine("OK: no findings.");
            return 0;
        }
        foreach (var r in results)
        {
            stdout.WriteLine($"[{SeverityLabel(r.Severity)}] {r.Section}.{r.Key}: {r.Message}");
        }
        return Engine.HasErrors(results) ? 1 : 0;
    }
}
