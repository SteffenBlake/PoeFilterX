using System.Collections.Immutable;
using PoeFilterX.Engine.Syntax;

namespace PoeFilterX.Engine.Results;

public sealed record ParseError(
    ImmutableArray<SyntaxDiagnostic> Diagnostics,
    SyntaxTree? PartialTree = null
)
{
    public ParseError(string code, string message) : this([new(code, message, string.Empty, default)])
    {
    }

    public ParseError At(string path, SourceSpan span)
    {
        return this with
        {
            Diagnostics = [.. Diagnostics.Select(diagnostic => diagnostic with { Path = path, Span = span })]
        };
    }
}
