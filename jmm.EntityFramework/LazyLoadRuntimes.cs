using System.Collections.Generic;
using System.Linq;

namespace jmm.EntityFramework
{
    public class LazyLoadRuntimes : Dictionary<string, List<long>>
    {
        // we could use concurrent collections and avoid locking, but we're
        // unlikely to have contention and it's only locking small/fast code
        private readonly object _lock = new object();
        public List<long> AddEntry(string location, long runTimeInMilliseconds)
        {
            lock (_lock)
            {
                List<long> listForLocation;
                if (this.TryGetValue(location, out listForLocation) == false)
                {
                    listForLocation = new List<long>();
                    Add(location, listForLocation);
                }
                listForLocation.Add(runTimeInMilliseconds);
                return listForLocation;
            }
        }

        public KeyValuePair<string, List<long>>[] GetAndClearRuntimes()
        {
            lock (_lock)
            {
                var runtimes = this.ToArray();
                this.Clear();
                return runtimes;
            }
        }
    }
}