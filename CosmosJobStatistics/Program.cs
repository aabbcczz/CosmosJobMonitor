using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using VcClient;
using VcClientExceptions;

namespace CosmosJobMonitor.CosmosJobStatistics
{
    class Program
    {
        
        static void Main(string[] args)
        {
            int exitCode = 0;

            try
            {
                CosmosJobMonitor.Share.CosmosJobMonitorUtility.SetDataDirectory(Properties.Settings.Default.DataDirectory);

                // get virtual clusters from configuration file
                var virtualClusters = GetVirtualClusters();

                foreach (var vc in virtualClusters)
                {
                    JobFetcher fetcher = new JobFetcher(vc);

                    // get jobs
                    fetcher.Fetch();
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Exception: {0}", ex);
                exitCode = -1;
            }
#if DEBUG
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
#endif
            Environment.Exit(exitCode);
        }

        private static IEnumerable<string> GetVirtualClusters()
        {
            if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.VirtualClusters))
            {
                var virtualClusters = Properties.Settings.Default.VirtualClusters.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                if (virtualClusters.Length > 0)
                {
                    return virtualClusters;
                }
            }

            throw new InvalidOperationException("please specify the virtual clusters where job is running on in the configuration file. Multiple virtual clusters can be separated by comma");
        }
    }
}
