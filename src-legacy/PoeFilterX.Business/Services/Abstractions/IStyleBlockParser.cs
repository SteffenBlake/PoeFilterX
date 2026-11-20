using PoeFilterX.Business.Models;

namespace PoeFilterX.Business.Services.Abstractions
{
    public interface IStyleBlockParser
    {
        IList<Action<FilterBlock>> Parse(TrackingStreamReader reader);
    }
}