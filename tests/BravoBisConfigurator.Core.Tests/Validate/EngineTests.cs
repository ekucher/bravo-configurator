using BravoBisConfigurator.Core.Ini;
using BravoBisConfigurator.Core.Schema;
using BravoBisConfigurator.Core.Validate;
using Xunit;

namespace BravoBisConfigurator.Core.Tests.Validate;

/// <summary>Ported 1:1 from internal/validate/engine_test.go.</summary>
public class EngineTests
{
    private static Document ParseDoc(string content) => Parser.Parse(content, ParseOptions.Default());

    [Fact]
    public void Validate_RequiredFieldMissing()
    {
        var s = new BravoBisConfigurator.Core.Schema.Schema
        {
            ProfileName = "t",
            Status = SchemaStatus.Draft,
            Sections = new List<SectionDef>
            {
                new() { Name = "model", Fields = new List<FieldDef> { new() { Key = "MODEL", Type = FieldType.String, Required = true } } },
            },
        };
        var doc = ParseDoc("[model]\nOTHER=x\n");
        var results = Engine.Validate(doc, s);
        Assert.True(Engine.HasErrors(results));

        var errors = results.Where(r => r.Severity == Severity.Error).ToList();
        Assert.Single(errors);
        Assert.Equal("MODEL", errors[0].Key);
    }

    [Fact]
    public void Validate_RequiredFieldPresent_NoError()
    {
        var s = new BravoBisConfigurator.Core.Schema.Schema
        {
            ProfileName = "t",
            Status = SchemaStatus.Draft,
            Sections = new List<SectionDef>
            {
                new() { Name = "model", Fields = new List<FieldDef> { new() { Key = "MODEL", Type = FieldType.String, Required = true } } },
            },
        };
        var results = Engine.Validate(ParseDoc("[model]\nMODEL=x\n"), s);
        Assert.False(Engine.HasErrors(results));
    }

    [Fact]
    public void Validate_PathExists_ValidAndInvalid()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var file = Path.Combine(dir.FullName, "f.txt");
            File.WriteAllText(file, "x");

            BravoBisConfigurator.Core.Schema.Schema FieldSchema(PathMode mode) => new()
            {
                ProfileName = "t",
                Status = SchemaStatus.Draft,
                Sections = new List<SectionDef>
                {
                    new()
                    {
                        Name = "model",
                        Fields = new List<FieldDef>
                        {
                            new() { Key = "P", Type = FieldType.Path, Validation = new ValidationRule { Kind = RuleKind.PathExists, PathMode = mode } },
                        },
                    },
                },
            };

            var docDir = ParseDoc($"[model]\nP={dir.FullName}\n");
            Assert.False(Engine.HasErrors(Engine.Validate(docDir, FieldSchema(PathMode.Dir))));
            Assert.True(Engine.HasErrors(Engine.Validate(docDir, FieldSchema(PathMode.File)))); // dir where file expected

            var docMissing = ParseDoc($"[model]\nP={Path.Combine(dir.FullName, "nope")}\n");
            Assert.True(Engine.HasErrors(Engine.Validate(docMissing, FieldSchema(PathMode.Either))));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void Validate_Regex_ValidAndInvalid()
    {
        var s = new BravoBisConfigurator.Core.Schema.Schema
        {
            ProfileName = "t",
            Status = SchemaStatus.Draft,
            Sections = new List<SectionDef>
            {
                new()
                {
                    Name = "scan",
                    Fields = new List<FieldDef>
                    {
                        new() { Key = "ext", Type = FieldType.String, Validation = new ValidationRule { Kind = RuleKind.Regex, Pattern = @"^\.[A-Za-z0-9]+$" } },
                    },
                },
            },
        };
        Assert.False(Engine.HasErrors(Engine.Validate(ParseDoc("[scan]\next=.jpg\n"), s)));
        Assert.True(Engine.HasErrors(Engine.Validate(ParseDoc("[scan]\next=jpg\n"), s)));
    }

    [Fact]
    public void Validate_Enum_ValidAndInvalid()
    {
        var s = new BravoBisConfigurator.Core.Schema.Schema
        {
            ProfileName = "t",
            Status = SchemaStatus.Draft,
            Sections = new List<SectionDef>
            {
                new()
                {
                    Name = "config",
                    Fields = new List<FieldDef>
                    {
                        new() { Key = "checkApp", Type = FieldType.Enum, Validation = new ValidationRule { Kind = RuleKind.Enum, Values = new List<string> { "on", "off" } } },
                    },
                },
            },
        };
        Assert.False(Engine.HasErrors(Engine.Validate(ParseDoc("[config]\ncheckApp=off\n"), s)));
        Assert.True(Engine.HasErrors(Engine.Validate(ParseDoc("[config]\ncheckApp=maybe\n"), s)));
    }

    [Fact]
    public void Validate_Range_ValidAndInvalid()
    {
        var s = new BravoBisConfigurator.Core.Schema.Schema
        {
            ProfileName = "t",
            Status = SchemaStatus.Draft,
            Sections = new List<SectionDef>
            {
                new()
                {
                    Name = "net",
                    Fields = new List<FieldDef>
                    {
                        new() { Key = "PORT", Type = FieldType.Int, Validation = new ValidationRule { Kind = RuleKind.Range, Min = 1, Max = 65535 } },
                    },
                },
            },
        };
        Assert.False(Engine.HasErrors(Engine.Validate(ParseDoc("[net]\nPORT=9001\n"), s)));
        Assert.True(Engine.HasErrors(Engine.Validate(ParseDoc("[net]\nPORT=70000\n"), s)));
    }

    [Fact]
    public void Validate_WarningSeverityDoesNotBlock()
    {
        var s = new BravoBisConfigurator.Core.Schema.Schema
        {
            ProfileName = "t",
            Status = SchemaStatus.Draft,
            Sections = new List<SectionDef>
            {
                new()
                {
                    Name = "net",
                    Fields = new List<FieldDef>
                    {
                        new() { Key = "PORT", Type = FieldType.Int, Validation = new ValidationRule { Kind = RuleKind.Range, Min = 1, Max = 65535, SeverityRaw = Severity.Warning } },
                    },
                },
            },
        };
        var results = Engine.Validate(ParseDoc("[net]\nPORT=70000\n"), s);
        Assert.False(Engine.HasErrors(results));
        Assert.Single(results);
        Assert.Equal(Severity.Warning, results[0].Severity);
    }

    [Fact]
    public void Validate_UnknownKey_WarningOnly()
    {
        var s = new BravoBisConfigurator.Core.Schema.Schema
        {
            ProfileName = "t",
            Status = SchemaStatus.Draft,
            Sections = new List<SectionDef>
            {
                new() { Name = "model", Fields = new List<FieldDef> { new() { Key = "MODEL", Type = FieldType.String } } },
            },
        };
        var results = Engine.Validate(ParseDoc("[model]\nMODEL=x\nFutureKey=y\n"), s);
        Assert.False(Engine.HasErrors(results));
        Assert.Contains(results, r => r.Key == "FutureKey" && r.Severity == Severity.Warning);
    }

    [Fact]
    public void Validate_UnknownKey_DeduplicatedAcrossDuplicateOccurrences()
    {
        var s = new BravoBisConfigurator.Core.Schema.Schema { ProfileName = "t", Status = SchemaStatus.Draft };
        var results = Engine.Validate(ParseDoc("[model]\nFutureKey=a\nFutureKey=b\n"), s);
        Assert.Single(results.Where(r => r.Key == "FutureKey"));
    }

    [Fact]
    public void Validate_TypeCoercion_InvalidIntBoolFloat()
    {
        var s = new BravoBisConfigurator.Core.Schema.Schema
        {
            ProfileName = "t",
            Status = SchemaStatus.Draft,
            Sections = new List<SectionDef>
            {
                new()
                {
                    Name = "s",
                    Fields = new List<FieldDef>
                    {
                        new() { Key = "I", Type = FieldType.Int },
                        new() { Key = "B", Type = FieldType.Bool },
                        new() { Key = "F", Type = FieldType.Float },
                    },
                },
            },
        };
        var results = Engine.Validate(ParseDoc("[s]\nI=notanint\nB=maybe\nF=notafloat\n"), s);
        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.Equal(Severity.Error, r.Severity));
    }

    [Fact]
    public void Validate_CleanRealWorldModelSchema_ZeroFindings()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var blogDir = Directory.CreateDirectory(Path.Combine(dir.FullName, "BLOG"));
            var modelFile = Path.Combine(dir.FullName, "model");
            File.WriteAllText(modelFile, "x");
            var bexchDir = Directory.CreateDirectory(Path.Combine(dir.FullName, "bravoexch"));

            var s = new BravoBisConfigurator.Core.Schema.Schema
            {
                ProfileName = "bravo",
                Status = SchemaStatus.Verified,
                Sections = new List<SectionDef>
                {
                    new()
                    {
                        Name = "model",
                        Fields = new List<FieldDef>
                        {
                            new() { Key = "MODEL", Type = FieldType.Path, Required = true, Validation = new ValidationRule { Kind = RuleKind.PathExists, PathMode = PathMode.Either } },
                            new() { Key = "BLOG", Type = FieldType.Path, Required = true, Validation = new ValidationRule { Kind = RuleKind.PathExists, PathMode = PathMode.Dir } },
                            new() { Key = "BEXCH", Type = FieldType.Path, Required = true, Validation = new ValidationRule { Kind = RuleKind.PathExists, PathMode = PathMode.Dir } },
                        },
                    },
                },
            };
            var doc = ParseDoc($"[model]\nMODEL={modelFile}\nBLOG={blogDir.FullName}\nBEXCH={bexchDir.FullName}\n");
            var results = Engine.Validate(doc, s);
            Assert.Empty(results);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
