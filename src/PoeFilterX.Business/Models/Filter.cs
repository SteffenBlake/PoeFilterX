using System.Text;

namespace PoeFilterX.Business.Models
{
    public class Filter
    {
        private IList<FilterBlock> FilterBlocks { get; } = new List<FilterBlock>();

        private IDictionary<string, IList<(int Rank, Action<FilterBlock> Command)>> Styles = new Dictionary<string, IList<(int, Action<FilterBlock>)>>();

        private int StyleCount = 0;

        public void AddFilterBlock(FilterBlock block)
        {
            FilterBlocks.Add(block);
        }

        public void AddStyle(string name, Action<FilterBlock> command)
        {
            if (!Styles.ContainsKey(name))
            {
                Styles[name] = new List<(int, Action<FilterBlock>)>();
            }

            Styles[name].Add((StyleCount++, command));
        }

        public async Task WriteAsync(TextWriter writer)
        {
            var builder = new StringBuilder();

            for (var n = FilterBlocks.Count -1; n >=0; n--)
            {
                var filterText = FilterBlocks[n].Compile(Styles).Trim();
                if (string.IsNullOrWhiteSpace(filterText))
                    continue;

                await writer.WriteLineAsync(filterText);
                await writer.WriteLineAsync();
            }
        }
    }
}
