using System.Collections.Immutable;
using Antlr4.Runtime;
using PoeFilterX.Engine.Diagnostics;

namespace PoeFilterX.Engine.Parsing;

internal sealed class ParserDiagnostics : BaseErrorListener
{
    internal sealed record Entry(
        IToken Token,
        string Code,
        string Message,
        ImmutableArray<string> ExpectedTokens,
        ImmutableArray<string> RuleStack
    );

    public List<Entry> Entries { get; } = [];

    public override void SyntaxError(
        TextWriter output,
        IRecognizer recognizer,
        IToken offendingSymbol,
        int line,
        int charPositionInLine,
        string msg,
        RecognitionException e
    )
    {
        var parser = (Parser)recognizer;
        var code = e switch
        {
            InputMismatchException => DiagnosticCodes.InputMismatch,
            NoViableAltException => DiagnosticCodes.NoViableAlternative,
            FailedPredicateException => DiagnosticCodes.FailedPredicate,
            _ => (parser.ErrorHandler as SyntaxErrorStrategy)?.Code ?? DiagnosticCodes.SyntaxError
        };
        var expectedTokens = e?.GetExpectedTokens() ?? parser.GetExpectedTokens();
        var expected = expectedTokens.ToArray().Select(parser.Vocabulary.GetDisplayName).ToImmutableArray();
        Entries.Add(new(offendingSymbol, code, msg, expected, [.. parser.GetRuleInvocationStack()]));
    }
}
