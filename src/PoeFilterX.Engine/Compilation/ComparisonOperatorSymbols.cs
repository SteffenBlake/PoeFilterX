namespace PoeFilterX.Engine.Compilation;

internal static class ComparisonOperatorSymbols
{
    public static string ToSymbol(this ComparisonOperator value)
    {
        return value switch
        {
            ComparisonOperator.Equal => "=",
            ComparisonOperator.ExactEqual => "==",
            ComparisonOperator.NotEqual => "!",
            ComparisonOperator.GreaterThan => ">",
            ComparisonOperator.LessThan => "<",
            ComparisonOperator.LessThanOrEqual => "<=",
            ComparisonOperator.GreaterThanOrEqual => ">=",
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };
    }
}
