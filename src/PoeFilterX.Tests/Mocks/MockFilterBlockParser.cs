using PoeFilterX.Business;
using PoeFilterX.Business.Models;
using PoeFilterX.Business.Services.Abstractions;

namespace PoeFilterX.Tests.Mocks
{
    public class MockFilterBlockParser : IFilterBlockParser
    {
        public async Task ReadBlockAsync(Filter filter, TrackingStreamReader reader, FilterBlock? parent = null)
        {
            _ = await reader.ReadLineAsync();
        }
    }
}