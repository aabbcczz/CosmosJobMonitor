using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;

namespace CosmosJobMonitor.Monitor
{
    [Serializable]
    public sealed class AlertingRule
    {
        public string SqlQuery { get; set; }

        public string MailBody { get; set; }

        public bool ShouldFilterJobName { get; set; }

        public static AlertingRule GenerateSample()
        {
            return new AlertingRule()
            {
                SqlQuery = "Select Id from dbo.JobStatistics where PNSeconds > 3600*1000",
                MailBody = "The jobs have used too many PN hours and they need to be reviewed and approved",
                ShouldFilterJobName = true,
            };
        }
    }
}
