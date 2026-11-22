using System.Collections.Immutable;
using PoeFilterX.Engine.Syntax;

namespace PoeFilterX.Engine.Checking;

public sealed record CheckResponse(
    int SchemaVersion,
    CheckResult? Result,
    ImmutableArray<SyntaxDiagnostic> Diagnostics
);
