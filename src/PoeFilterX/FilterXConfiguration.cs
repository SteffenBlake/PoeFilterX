namespace PoeFilterX
{
    public class FilterXConfiguration
    {
        public string? P { get; set; }
        public string? Path { get; set; }
        public string UsedPath => P ?? Path ?? ".FilterX";


        public string? O { get; set; }
        public string? Output { get; set; }
        public string OutputPath => O ?? Output ?? System.IO.Path.GetFileNameWithoutExtension(UsedPath) + ".filter";
    }
}
