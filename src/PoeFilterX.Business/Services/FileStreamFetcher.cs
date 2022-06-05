namespace PoeFilterX.Business.Services
{
    public class FileStreamFetcher : IStreamFetcher
    {
        public TrackingStreamReader Fetch(string filePath)
        {
            var normalizedPath = new DirectoryInfo(filePath).FullName;
            Console.WriteLine($"Opening '{normalizedPath}'");
            if (!File.Exists(normalizedPath))
                throw new ParserException($"File not found: '{normalizedPath}'");
            return new TrackingStreamReader(normalizedPath);
        }
    }
}