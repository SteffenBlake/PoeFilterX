using System.Reflection;

namespace PoeFilterX.Common
{
    public static class SystemHelper
    {
        public static string? GetAssemblyVersionStr()
        {
            var appName = Assembly.GetExecutingAssembly().GetName();
            var currentVersionRaw = appName.Version;
            return currentVersionRaw == null ? null : 
                $"{currentVersionRaw.Major}.{currentVersionRaw.Minor}.{currentVersionRaw.Build}";
        }

        public static string? GetSystemPlatform()
        {
            var platform =
               OperatingSystem.IsWindows() ? "win-" :
               OperatingSystem.IsMacOS() ? "osx-" :
               OperatingSystem.IsLinux() ? "linux-" :
               null;

            if (platform == null)
            {
                Console.Error.WriteLine("Unable to auto-detect platform. Please specify a supported platform via the '--p/--platform' argument");
                return null;
            }

            platform += System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString().ToLower();

            return platform;
        }
    }
}
