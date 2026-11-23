using System.Text.Json;
using System.Text.Json.Nodes;
using PoeFilterX.Engine.Checking;
using PoeFilterX.Engine.Compilation;
using PoeFilterX.Engine.Diagnostics;
using PoeFilterX.Engine.Projects;
using PoeFilterX.Engine.Results;

namespace PoeFilterX.LanguageServer;

internal static class CheckFeatures
{
    public static JsonNode Inputs()
    {
        var result = new JsonArray();
        var facts = Enum.GetNames<FilterFact>().ToHashSet(StringComparer.Ordinal);
        foreach (var operation in FilterOperationsCodec.Operations)
        {
            if (operation.Name == nameof(FilterFact.AreaLevel) || !facts.Contains(operation.Name))
            {
                continue;
            }

            var kind = operation.Type == "bool?" ? "boolean" : operation.Type.Contains("<int>", StringComparison.Ordinal)
                ? "number" : "text";
            string[] choices = operation.Name switch
            {
                nameof(FilterFact.Rarity) => Enum.GetNames<FilterRarity>(),
                nameof(FilterFact.HasInfluence) => Enum.GetNames<InfluenceType>(),
                _ => []
            };
            result.Add(new JsonObject
            {
                ["name"] = operation.Name,
                ["kind"] = kind,
                ["choices"] = new JsonArray([.. choices.Select(value => (JsonNode?)JsonValue.Create(value))])
            });
        }

        return result;
    }

    public static JsonNode? Check(
        EditorContext context,
        JsonElement parameters,
        CancellationToken token
    )
    {
        var gameText = parameters.Property("game").Text();
        if (!Enum.TryParse<GameVersion>(gameText, true, out var game) || !Enum.IsDefined(game))
        {
            throw ProtocolJson.InvalidParameters("Select Poe1 or Poe2.");
        }

        var facts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var overrides = parameters.Property("facts");
        if (overrides.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in overrides.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    throw ProtocolJson.InvalidParameters("Item fact overrides must be strings.");
                }

                facts.Add(property.Name, property.Value.GetString()!);
            }
        }

        var result = context.HasEntrypoint
            ? ItemChecker.Check(context.Project, parameters.Property("item").Text(), game,
                parameters.Property("areaLevel").Number(ItemChecker.DefaultAreaLevel(game)), ProjectConfiguration.Read(null, context.Project.Settings), facts, token)
            : new ParseError(DiagnosticCodes.InvalidItem, context.DiscoveryWarning ?? "Select a filter entrypoint first.");
        var response = new CheckResponse(1, result.Value as CheckResult,
            result is ParseError error ? error.Diagnostics : []);

        return JsonSerializer.SerializeToNode(response, CheckJsonContext.Default.CheckResponse);
    }
}
