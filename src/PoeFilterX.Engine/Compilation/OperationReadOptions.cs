namespace PoeFilterX.Engine.Compilation;

internal readonly record struct OperationReadOptions(
    string Name,
    int Minimum,
    int Maximum,
    int DefaultAlpha
);
