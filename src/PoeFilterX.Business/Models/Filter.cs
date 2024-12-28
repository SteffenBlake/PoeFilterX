namespace PoeFilterX.Business.Models
{
    public class Filter
    {
        private IList<FilterBlock> FilterBlocks { get; } = new List<FilterBlock>();

        private readonly IDictionary<string, IList<(int Rank, Action<FilterBlock> Command)>> _styles = new Dictionary<string, IList<(int, Action<FilterBlock>)>>();

        private int _styleCount;

        public void AddFilterBlock(FilterBlock block)
        {
            FilterBlocks.Add(block);
        }

        public void AddStyle(string name, Action<FilterBlock> command)
        {
            if (!_styles.ContainsKey(name))
            {
                _styles[name] = new List<(int, Action<FilterBlock>)>();
            }

            _styles[name].Add((_styleCount++, command));
        }

        public async Task WriteAsync(TextWriter writer)
        {
            for (var n = FilterBlocks.Count -1; n >=0; n--)
            {
                var filterText = "\t" + FilterBlocks[n].Compile(_styles).Trim();
                if (string.IsNullOrWhiteSpace(filterText))
                {
                    continue;
                }

                await writer.WriteLineAsync(FilterBlocks[n].Show ? "Show" : "Hide");

                await writer.WriteLineAsync(filterText);
                await writer.WriteLineAsync();
            }
        }
    }
}
