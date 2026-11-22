using System.Text.Json.Serialization;

namespace PoeFilterX.Engine.Checking;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(CheckResponse))]
public partial class CheckJsonContext : JsonSerializerContext;
