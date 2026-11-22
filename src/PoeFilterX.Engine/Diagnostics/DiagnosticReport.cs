using System.Collections.Immutable;
using PoeFilterX.Engine.Syntax;

namespace PoeFilterX.Engine.Diagnostics;

public sealed record DiagnosticReport(
    SyntaxDiagnostic Diagnostic,
    ImmutableArray<DiagnosticRelatedLocation> ImportTrace
);
