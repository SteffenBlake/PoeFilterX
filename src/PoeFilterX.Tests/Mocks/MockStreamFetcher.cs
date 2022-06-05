using PoeFilterX.Business;
using PoeFilterX.Business.Services;

namespace PoeFilterX.Tests.Mocks
{
    internal class MockStreamFetcher : IStreamFetcher
    {
        private IDictionary<string, string> Files { get; }
        internal MockStreamFetcher(IDictionary<string, string> files)
        {
            Files = files;
        }

        public TrackingStreamReader Fetch(string filePath)
        {
            var data = Files[filePath];

            var memStream = new MemoryStream();

            using var writer = new StreamWriter(memStream);
            writer.Write(data);
            writer.Flush();

            memStream.Position = 0;
            return new TrackingStreamReader(filePath, memStream);
        }
    }
}
