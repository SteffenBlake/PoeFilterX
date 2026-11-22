using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using StreamJsonRpc;

namespace PoeFilterX.LanguageServer;

[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(JsonNode))]
[JsonSerializable(typeof(JsonObject))]
[JsonSerializable(typeof(JsonArray))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int?))]
[JsonSerializable(typeof(int[]))]
[JsonSerializable(typeof(RequestId))]
internal partial class ProtocolJsonContext : JsonSerializerContext;
