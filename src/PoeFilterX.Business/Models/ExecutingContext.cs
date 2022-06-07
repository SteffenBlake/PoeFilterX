namespace PoeFilterX.Business.Models
{
    public class ExecutingContext
    {
        private Dictionary<string, IList<string>> UsingLinks { get; } = new();

        /// <summary>
        /// Attempts to add a Using link in the App Context. 
        /// Will return false if this causes a circular dependency, and won't add it to the context
        /// </summary>
        /// <param name="from">Fully resolved Path of the File</param>
        /// <param name="to">Fully resolved path of the using'd File</param>
        /// <returns></returns>
        public bool TryAddUsing(string from, string to)
        {
            var normalizedFrom = new DirectoryInfo(from).FullName;
            var normalizedTo = new DirectoryInfo(to).FullName;

            if (IsCircular(normalizedFrom, normalizedTo))
                return false;

            if (!UsingLinks.ContainsKey(normalizedFrom))
                UsingLinks.Add(normalizedFrom, new List<string>());

            if (UsingLinks[normalizedFrom].Contains(normalizedTo))
                return true;

            UsingLinks[normalizedFrom].Add(normalizedTo);

            return true;

        }

        private bool IsCircular(string from, string to)
        {
            if (from == to)
                return true;

            // to file doesn't have any dependencies at all
            return 
                UsingLinks.ContainsKey(to) && 
                UsingLinks[to].Any(next => IsCircular(from, next));
        }
    } 
}
