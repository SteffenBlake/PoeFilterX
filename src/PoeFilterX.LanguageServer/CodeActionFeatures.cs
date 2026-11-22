using System.Collections.Immutable;
using System.Text.Json.Nodes;
using PoeFilterX.Engine.Syntax;

namespace PoeFilterX.LanguageServer;

internal sealed class CodeActionFeatures(ImmutableArray<ICodeActionProvider> providers)
{
    public JsonArray GetActions(
        EditorContext context,
        SourceSpan range,
        CancellationToken cancellationToken
    )
    {
        var result = new JsonArray();
        foreach (var provider in providers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var action in provider.GetActions(context, range, cancellationToken))
            {
                result.Add(action?.DeepClone());
            }
        }

        return result;
    }
}
