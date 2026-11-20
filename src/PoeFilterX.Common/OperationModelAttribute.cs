using System;

namespace PoeFilterX.Common;

[AttributeUsage(AttributeTargets.Class)]
public sealed class OperationModelAttribute(
    string codecName,
    OperationLanguage kind = OperationLanguage.Filter
) : Attribute
{
    public const string MetadataName = "PoeFilterX.Common.OperationModelAttribute";

    public string CodecName { get; } = codecName;

    public OperationLanguage Kind { get; } = kind;
}
