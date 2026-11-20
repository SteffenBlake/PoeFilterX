using System;

namespace PoeFilterX.Common;

[AttributeUsage(AttributeTargets.Property)]
public sealed class OperationAttribute : Attribute
{
    public const string MetadataName = "PoeFilterX.Common.OperationAttribute";

    public string? Name { get; set; }

    public string? OutputName { get; set; }

    public string? Target { get; set; }

    public string? Reader { get; set; }

    public string? Writer { get; set; }

    public int DefaultAlpha { get; set; } = 255;

    public int Minimum { get; set; }

    public int Maximum { get; set; } = int.MaxValue;

    public bool SkipOutput { get; set; }
}
