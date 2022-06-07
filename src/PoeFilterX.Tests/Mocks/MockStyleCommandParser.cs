using PoeFilterX.Business.Models;
using PoeFilterX.Business.Services.Abstractions;

namespace PoeFilterX.Tests.Mocks
{
    public class MockStyleCommandParser : IStyleCommandParser
    {
        public IDictionary<string, Action<FilterBlock>?> Actions { get; } = new Dictionary<string, Action<FilterBlock>?>();
        public IDictionary<string, int> Calls { get; } = new Dictionary<string, int>();

        public Action<FilterBlock>? Parse(string runningArgs)
        {
            if (!Calls.ContainsKey(runningArgs))
                Calls[runningArgs] = 0;

            Calls[runningArgs]++;

            return Actions.ContainsKey(runningArgs) ? Actions[runningArgs] : null;

        }
    }
}
