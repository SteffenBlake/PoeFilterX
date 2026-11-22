namespace PoeFilterX.Generator;

internal sealed record GeneratorProperty(
    string Name,
    string Type,
    string Input,
    string Output,
    string Reader,
    string Writer,
    string Target,
    int Minimum,
    int Maximum,
    int DefaultAlpha,
    bool SkipOutput
);
