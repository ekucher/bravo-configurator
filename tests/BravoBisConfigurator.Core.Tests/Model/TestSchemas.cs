using BravoBisConfigurator.Core.Schema;

namespace BravoBisConfigurator.Core.Tests;

/// <summary>Shared test fixture, mirroring internal/app/model_test.go's testSchema().</summary>
internal static class TestSchemas
{
    public static BravoBisConfigurator.Core.Schema.Schema Bravo() => new()
    {
        ProfileName = "bravo",
        Status = SchemaStatus.Verified,
        Sections = new List<SectionDef>
        {
            new()
            {
                Name = "model",
                Label = "Model",
                Fields = new List<FieldDef>
                {
                    new() { Key = "MODEL", Label = "Model path", Type = FieldType.Path, Required = true },
                    new() { Key = "BLOG", Label = "BLOG dir", Type = FieldType.Path, Required = true },
                },
            },
        },
    };
}
