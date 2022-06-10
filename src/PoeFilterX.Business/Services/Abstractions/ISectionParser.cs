using PoeFilterX.Business.Models;

namespace PoeFilterX.Business.Services.Abstractions
{
    public interface ISectionParser
    {
        string FileExtension { get; }
        Task ParseAsync(Filter filter, TrackingStreamReader reader, FilterBlock? parent = null);
    }
}