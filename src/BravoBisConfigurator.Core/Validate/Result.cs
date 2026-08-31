using BravoBisConfigurator.Core.Schema;

namespace BravoBisConfigurator.Core.Validate;

/// <summary>
///  One finding: either a schema-defined field failing its
///  required/type/validation check, or an unrecognized key in the file.
/// </summary>
public sealed record Result(string Section, string Key, Severity Severity, string Message);
