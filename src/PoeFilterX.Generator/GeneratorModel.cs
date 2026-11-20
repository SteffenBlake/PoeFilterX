using System;
using System.Collections.Immutable;
using System.Linq;
using PoeFilterX.Common;

namespace PoeFilterX.Generator;

internal sealed record GeneratorModel(
    string Namespace,
    string Name,
    string Codec,
    OperationLanguage Kind,
    ImmutableArray<GeneratorProperty> Properties
)
{
    public bool Equals(GeneratorModel? other)
    {
        return other is not null && Namespace == other.Namespace && Name == other.Name
            && Codec == other.Codec && Kind == other.Kind && Properties.SequenceEqual(other.Properties);
    }

    public override int GetHashCode()
    {
        var hash = StringComparer.Ordinal.GetHashCode(Namespace);
        hash = unchecked(hash * 31 + StringComparer.Ordinal.GetHashCode(Name));
        hash = unchecked(hash * 31 + StringComparer.Ordinal.GetHashCode(Codec));
        hash = unchecked(hash * 31 + (int)Kind);
        foreach (var property in Properties)
        {
            hash = unchecked(hash * 31 + property.GetHashCode());
        }

        return hash;
    }

}
