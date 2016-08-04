using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace CosmosJobMonitor.Share
{
    public static class CosmosJobMonitorUtility
    {
        public static void SetDataDirectory(string dataDirectory)
        {
            var fullDataDirectory = Path.GetFullPath(dataDirectory);
            AppDomain.CurrentDomain.SetData("DataDirectory", fullDataDirectory);
        }
    }
}
