using PoeFilterX.Business.Models;

namespace PoeFilterX.Business.Services.Abstractions
{
    public interface IStyleCommandParser
    {
        Action<FilterBlock>? Parse(string runningArgs);
    }
}