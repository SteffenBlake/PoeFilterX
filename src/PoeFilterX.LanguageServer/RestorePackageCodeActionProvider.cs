using System.Text.Json.Nodes;
using PoeFilterX.Engine.Diagnostics;
using PoeFilterX.Engine.Syntax;

namespace PoeFilterX.LanguageServer;

internal sealed class RestorePackageCodeActionProvider : ICodeActionProvider
{
    public JsonArray GetActions(
        EditorContext context,
        SourceSpan range,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!context.HasEntrypoint || !context.Project.Packages.Diagnostics.Any(diagnostic => diagnostic.Code == DiagnosticCodes.RestoreRequired))
        {
            return [];
        }

        return [(JsonNode)new JsonObject
        {
            ["title"] = "Restore PoeFilterX packages",
            ["kind"] = "quickfix",
            ["command"] = new JsonObject
            {
                ["title"] = "Restore PoeFilterX packages",
                ["command"] = "poefilterx.restore",
                ["arguments"] = new JsonArray(ProtocolJson.FileUri(context.Project.EntryPoint))
            }
        }];
    }
}
