using PoeFilterX.Business.Models;
using PoeFilterX.Business.Services.Abstractions;

namespace PoeFilterX.Business.Services
{
    public class FilterXParser : ISectionParser
    {
        public IStreamFetcher StreamFetcher { get; }
        private IFilterBlockParser BlockParser { get; }

        public string FileExtension => ".filterx";

        public FilterXParser(IStreamFetcher streamFetcher, IFilterBlockParser blockParser)
        {
            StreamFetcher = streamFetcher ?? throw new ArgumentNullException(nameof(streamFetcher));
            BlockParser = blockParser ?? throw new ArgumentNullException(nameof(blockParser));
        }

        public async Task ParseAsync(Filter filter, TrackingStreamReader reader, FilterBlock? parent = null)
        {
            while (!reader.EndOfStream)
            {
                await BlockParser.ReadBlockAsync(filter, reader, parent);
            }
        }
    }
}
