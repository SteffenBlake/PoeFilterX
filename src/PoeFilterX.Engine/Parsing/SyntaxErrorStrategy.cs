using Antlr4.Runtime;
using PoeFilterX.Engine.Diagnostics;

namespace PoeFilterX.Engine.Parsing;

/// <summary>Adds codes to ANTLR's insertion/deletion events without changing its messages or recovery.</summary>
internal sealed class SyntaxErrorStrategy : DefaultErrorStrategy
{
    public string Code { get; private set; } = DiagnosticCodes.SyntaxError;

    protected override void ReportMissingToken(Parser recognizer)
    {
        Code = DiagnosticCodes.MissingToken;
        try
        {
            base.ReportMissingToken(recognizer);
        }
        finally
        {
            Code = DiagnosticCodes.SyntaxError;
        }
    }

    protected override void ReportUnwantedToken(Parser recognizer)
    {
        Code = DiagnosticCodes.UnexpectedToken;
        try
        {
            base.ReportUnwantedToken(recognizer);
        }
        finally
        {
            Code = DiagnosticCodes.SyntaxError;
        }
    }
}
