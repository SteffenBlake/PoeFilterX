using Antlr4.Runtime;
using PoeFilterX.Engine.Diagnostics;
using PoeFilterX.Engine.Syntax;

namespace PoeFilterX.Engine.Checking;

internal sealed class CopiedItemErrorListener(List<SyntaxDiagnostic> diagnostics) : BaseErrorListener
{
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
        diagnostics.Add(new(DiagnosticCodes.InvalidItem, $"Invalid copied item: {msg}. Use English item text copied with Ctrl+C in game.",
            "<item>", new(Math.Max(0, offendingSymbol.StartIndex), Math.Max(0, offendingSymbol.Text?.Length ?? 0))));
    }
}
