using System.Text.Json.Serialization;

namespace PoeFilterX.Engine.Packages;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(PackageLock))]
internal partial class PackageJsonContext : JsonSerializerContext;
