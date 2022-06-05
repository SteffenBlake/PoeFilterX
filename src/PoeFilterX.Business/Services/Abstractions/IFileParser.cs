using PoeFilterX.Business.Models;

namespace PoeFilterX.Business.Services.Abstractions
{
    public interface IFileParser
    {
        public Task ParseAsync(Filter filter, string path);
    }
}
