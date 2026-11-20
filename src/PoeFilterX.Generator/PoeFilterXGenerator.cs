using Microsoft.CodeAnalysis;

namespace PoeFilterX.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class PoeFilterXGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Generation pipelines will be added as part of the rebuild.
    }
}
