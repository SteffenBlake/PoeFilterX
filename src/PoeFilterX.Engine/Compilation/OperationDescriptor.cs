namespace PoeFilterX.Engine.Compilation;

public sealed record OperationDescriptor(
    string Name,
    string Type,
    string Reader,
    int Minimum,
    int Maximum
);
