namespace PoeFilterX
{
    public class FilterXConfiguration
    {
        public string? P { get; set; }
        public string? Path { get; set; }
        public string? UsedPath()
        {
            var usedPath = P ?? Path;
            if (usedPath != null)
            {
                return usedPath;
            }

            var matches = Directory.GetFiles(Environment.CurrentDirectory, "*.filterx");
            if (matches is { Length: 1 })
            {
                return matches[0];
            }

            Console.Error.WriteLine("Unable to automatically detect a valid .filterx project to build from");
            return null;
        }

        public string? O { get; set; }
        public string? Output { get; set; }
        public string OutputPath() => O ?? Output ?? System.IO.Path.GetFileNameWithoutExtension(UsedPath()) + ".filter";

    }
}
