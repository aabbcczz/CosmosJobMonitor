using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CosmosJobMonitor.Monitor
{
    [Serializable]
    public sealed class AlertingRules
    {
        public List<AlertingRule> Rules { get; set; }

        public MailSettings MailSettings { get; set; }

        public List<string> JobNameFilters { get; set; }

        public int LookbackDays { get; set; }

        public static AlertingRules GenerateSample()
        {
            return new AlertingRules()
            {
                Rules = new List<AlertingRule>()
                {
                    AlertingRule.GenerateSample()
                },

                MailSettings = MailSettings.GenerateSample(),

                JobNameFilters = new List<string>()
                {
                    "APPROVED_BY_TECHBOARD",
                    "APPROVED_BY_ADMIN",
                },

                LookbackDays = 3,
            };
        }
    }
}
