namespace PoeFilterX.Update
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var executingPath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            var executingFolder = Path.GetDirectoryName(executingPath);
            if (executingFolder == null)
            {
                Console.Error.WriteLine("Unable to discern executing folder. Something went wrong.");
                return;
            }

            var tempFolder = Path.Combine(executingFolder, "temp");
            if (!Directory.Exists(tempFolder))
            {
                Console.Error.WriteLine("Temp folder is missing, ensure you run updater via PoeFilterX executable, not directly, to start bootstrapping process.");
                return;
            }

            var targetExeFileName = Path.GetExtension(executingPath) == ".exe" ?
                "PoeFilterX.exe" : "PoeFilterX";

            var fromPath = Path.Combine(tempFolder, targetExeFileName);
            var toPath = Path.Combine(executingFolder, targetExeFileName);

            Console.WriteLine($"Copying over update:\n\tFrom: {fromPath}\n\tTo: {toPath}");
            File.Copy(fromPath, toPath, true);

            Console.WriteLine("Cleaning up temp folder...");

            Directory.Delete(tempFolder, true);
            Console.WriteLine("Update complete! Run 'PoeFilterX Version' to verify!");
        }
    }
}