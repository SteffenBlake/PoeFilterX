using PoeFilterX.Business.Models;

namespace PoeFilterX.Business.Services.Abstractions
{
    public interface IFilterCommandParser
    {
        Action<FilterBlock>? Parse(string[] args);
    }

}