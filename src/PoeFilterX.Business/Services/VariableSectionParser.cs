using PoeFilterX.Business.Extensions;
using PoeFilterX.Business.Models;
using PoeFilterX.Business.Services.Abstractions;
using System.Text.Json;

namespace PoeFilterX.Business.Services
{
    public class VariableSectionParser : ISectionParser
    {
        private IVariableStore VariableStore { get; }
        public VariableSectionParser(IVariableStore variableStore)
        {
            VariableStore = variableStore ?? throw new ArgumentNullException(nameof(variableStore));
        }

        public string FileExtension => ".json";

        public async Task ParseAsync(Filter filter, TrackingStreamReader reader, FilterBlock? parent = null)
        {
            var newDataRaw = await reader.ReadToEndAsync();

            var newData = JsonSerializer.Deserialize<Dictionary<string, string[]>>(newDataRaw) ?? new();

            foreach (var kvp in newData)
            {
                var args = kvp.Value.SelectMany(s => s.ToArgs()).ToList();
                
                foreach (var arg in args)
                {
                    VariableStore.Add(kvp.Key, arg);
                }
            }
        }
    }
}
