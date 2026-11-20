namespace PoeFilterX.Engine.Diagnostics;

/// <summary>Stable application codes. ANTLR runtime recognition errors do not have numeric codes.</summary>
public static class DiagnosticCodes
{
    public const string InvalidToken = "PFX1001";
    public const string SyntaxError = "PFX1002";
    public const string MissingToken = "PFX1003";
    public const string UnexpectedToken = "PFX1004";
    public const string InputMismatch = "PFX1005";
    public const string NoViableAlternative = "PFX1006";
    public const string FailedPredicate = "PFX1007";
    public const string UnterminatedComment = "PFX1008";
    public const string NestingLimit = "PFX1009";
    public const string UnterminatedString = "PFX1010";
    public const string InvalidEscape = "PFX1011";
    public const string UnknownOperation = "PFX3001";
    public const string InvalidArgumentCount = "PFX3002";
    public const string InvalidArgument = "PFX3003";
    public const string VariableExpansion = "PFX3004";
    public const string UndeclaredStyle = "PFX3005";
    public const string BuildFailure = "PFX0001";
}
