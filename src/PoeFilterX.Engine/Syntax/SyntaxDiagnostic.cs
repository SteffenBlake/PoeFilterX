using System.Collections.Immutable;

namespace PoeFilterX.Engine.Syntax;

public sealed record SyntaxDiagnostic(string Code, string Message, string Path, SourceSpan Span)
{
    public DiagnosticSeverity Severity { get; init; } = DiagnosticSeverity.Error;

    public ImmutableArray<string> ExpectedTokens { get; init; } = [];

    /// <summary>ANTLR grammar rules, innermost first. No generated parser objects are retained.</summary>
    public ImmutableArray<string> RuleStack { get; init; } = [];
}
