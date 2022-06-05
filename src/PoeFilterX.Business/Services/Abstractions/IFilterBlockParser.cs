using PoeFilterX.Business.Models;

namespace PoeFilterX.Business.Services.Abstractions
{
    public interface IFilterBlockParser
    {
        Task ReadBlockAsync(Filter filter, TrackingStreamReader reader, FilterBlock? parent = null);
    }
}