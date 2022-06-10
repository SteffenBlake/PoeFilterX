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
        public string OutputPath()
        {
            var output = O ?? Output;
            if (output == null)
            {
                var usedPath = UsedPath();
                var dir = System.IO.Path.GetDirectoryName(usedPath);
                var file = System.IO.Path.GetFileNameWithoutExtension(usedPath) + ".filter";
                return System.IO.Path.Combine(dir, file);
            }

            return output;
        }
    }
}
