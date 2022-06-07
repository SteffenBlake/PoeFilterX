using PoeFilterX.Business.Models;
using PoeFilterX.Business.Services.Abstractions;

namespace PoeFilterX.Business.Services
{
    public class FileParser : IFileParser
    {
        private IStreamFetcher StreamFetcher { get; }
        private IEnumerable<ISectionParser> SectionParsers { get; }

        public FileParser(IStreamFetcher streamFetcher, IEnumerable<ISectionParser> sectionParsers)
        {
            StreamFetcher = streamFetcher ?? throw new ArgumentNullException(nameof(streamFetcher));
            SectionParsers = sectionParsers ?? throw new ArgumentNullException(nameof(sectionParsers));
        }

        public async Task ParseAsync(Filter filter, string path)
        {
            path = path.Trim();
            if (string.IsNullOrEmpty(path))
                throw new ParserException("Missing Path for using statement.");

            var fileExtension = Path.GetExtension(path);

            var parser = SectionParsers.SingleOrDefault(p => p.FileExtension == fileExtension);

            if (parser == null)
                throw new ParserException($"Unrecognized file extension for path '{path}'");

            using var reader = StreamFetcher.Fetch(path);

            try 
            {
                await parser.ParseAsync(filter, reader);
            }
            catch (ParserException ex)
            {
                await Console.Error.WriteLineAsync($"Error in file '{path}', line #{reader.Line}:\n\t {ex.Message}");
                Environment.Exit(1);
            }
        }
    }
}
