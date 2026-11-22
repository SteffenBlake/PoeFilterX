using PoeFilterX.Engine.Syntax;

namespace PoeFilterX.Engine.Projects;

/// <summary>An import occurrence. Repeated imports retain distinct edges and source positions.</summary>
public sealed record ProjectImport(string SourcePath, string TargetPath, ImportSyntax Syntax);
