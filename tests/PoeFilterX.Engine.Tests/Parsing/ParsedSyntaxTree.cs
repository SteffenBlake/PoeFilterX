using PoeFilterX.Engine.Results;
using PoeFilterX.Engine.Syntax;

namespace PoeFilterX.Engine.Tests.Parsing;

internal static class ParsedSyntaxTree
{
    public static SyntaxTree From(Result<SyntaxTree> result)
    {
        return result switch
        {
            SyntaxTree tree => tree,
            ParseError { PartialTree: { } tree } => tree,
            _ => throw new AssertionException("Expected a complete or recoverable syntax tree.")
        };
    }
}
