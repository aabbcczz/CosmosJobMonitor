using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CosmosJobMonitor.Monitor
{
    [Serializable]
    public sealed class MailSettings
    {
        public string ExchangeServerUrl { get; set; }

        public string ExchangeUserName { get; set; }

        public string ExchangeUserPassword { get; set; }

        public bool AutoDiscoverUrl { get; set; }

        public string From { get; set; }
        public string ToSuffix { get; set; }
        public List<string> CarbonCopy { get; set; }

        public static MailSettings GenerateSample()
        {
            return new MailSettings()
            {
                ExchangeServerUrl = "https://outlook.office365.com/EWS/Exchange.asmx",
                ExchangeUserName = "zhxiao@microsoft.com",
                ExchangeUserPassword = "xxxxx",
                AutoDiscoverUrl = true,
                From = "xxx@microsoft.com",
                ToSuffix = "@microsoft.com",
                CarbonCopy = new List<string>(){ "yyy@microsoft.com", "zzz@microsoft.com" },
            };
        }
    }
}
