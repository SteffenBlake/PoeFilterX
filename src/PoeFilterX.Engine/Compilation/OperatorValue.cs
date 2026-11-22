namespace PoeFilterX.Engine.Compilation;

public readonly record struct OperatorValue<T>(ComparisonOperator Operator, T Value);
