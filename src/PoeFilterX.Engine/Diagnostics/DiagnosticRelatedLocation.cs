using PoeFilterX.Engine.Syntax;

namespace PoeFilterX.Engine.Diagnostics;

public sealed record DiagnosticRelatedLocation(string Path, SourceSpan Span, string Message);
