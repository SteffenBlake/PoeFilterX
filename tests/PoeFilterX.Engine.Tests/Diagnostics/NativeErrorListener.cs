using Antlr4.Runtime;

namespace PoeFilterX.Engine.Tests.Diagnostics;

internal sealed class NativeErrorListener : BaseErrorListener
{
    public List<string> Messages { get; } = [];

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
        Messages.Add(msg);
    }
}
