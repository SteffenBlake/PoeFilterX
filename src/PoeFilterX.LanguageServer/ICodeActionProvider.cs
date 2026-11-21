using System.Text.Json.Nodes;
using PoeFilterX.Engine.Syntax;

namespace PoeFilterX.LanguageServer;

internal interface ICodeActionProvider
{
    JsonArray GetActions(
        EditorContext context,
        SourceSpan range,
        CancellationToken cancellationToken
    );
}
