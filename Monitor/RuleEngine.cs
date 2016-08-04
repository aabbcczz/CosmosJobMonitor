using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Net.Mail;
using System.Net;
using System.Security;

using CosmosJobMonitor.Share;
using VcClient;
using System.Data;
using Microsoft.Exchange.WebServices.Data;


namespace CosmosJobMonitor.Monitor
{
    sealed class RuleEngine
    {
        private const string IdColumnName = "Id";

        private readonly string _databaseConnectionString;
        private readonly int _commandTimeout;

        public delegate SecureString GetPasswordDelegate(string user);

        private readonly GetPasswordDelegate _getPasswordDelegate;

        public RuleEngine(string databaseConnectionString, int commandTimeout, GetPasswordDelegate getPasswordDelegate)
        {
            if (string.IsNullOrWhiteSpace(databaseConnectionString))
            {
                throw new ArgumentNullException("database connection string is empty");
            }

            _databaseConnectionString = databaseConnectionString;
            _commandTimeout = commandTimeout;
            _getPasswordDelegate = getPasswordDelegate;
        }

        public void Execute(AlertingRules rules, out List<Exception> exceptions)
        {
            List<Tuple<AlertingRule, List<Guid>>> allJobIds = new List<Tuple<AlertingRule, List<Guid>>>();
            exceptions = new List<Exception>();

            foreach (var rule in rules.Rules)
            {
                try
                {
                    var jobIds = ExecuteRule(rule);

                    if (jobIds.Any())
                    {
                        allJobIds.Add(Tuple.Create(rule, jobIds));
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }

            // get all user aliases we are tracking
            var userAliases = GetUserAliasesForTracking().ToDictionary(a => a);

            // get all job statistics for all rules
            var allJobStatistics = allJobIds
                .Select(t => Tuple.Create(t.Item1, GetJobStatistics(t.Item1, t.Item2, rules.JobNameFilters, userAliases, rules.LookbackDays)))
                .Where(t => t.Item2.Any())
                .ToList();

            if (allJobStatistics.Any())
            {
                SendEmail(rules.MailSettings, allJobStatistics);
            }
        }

        private List<JobStatistic> GetJobStatistics(
            AlertingRule rule, 
            IEnumerable<Guid> jobIds, 
            IEnumerable<string> jobNameFilters, 
            IDictionary<string, string> userAliases,
            int lookbackDays)
        {
            using (var context = new DatabaseObjectsDataContext(_databaseConnectionString))
            {
                DateTime minimumJobEndTime = DateTime.UtcNow.AddDays(-lookbackDays);

                return jobIds
                    .Select(id => context.JobStatistics.Where(stat => stat.Id == id).First())
                    .Where(jobStat => jobStat.EndTime == null || jobStat.EndTime > minimumJobEndTime)
                    .Where(jobStat => !rule.ShouldFilterJobName || jobNameFilters.All(filter => jobStat.Name.IndexOf(filter, StringComparison.InvariantCultureIgnoreCase) < 0))
                    .Where(jobStat => userAliases.ContainsKey(jobStat.TrueUserName.ToLowerInvariant()))
                    .ToList();
            }
        }

        private List<string> GetUserAliasesForTracking()
        {
            using (var context = new DatabaseObjectsDataContext(_databaseConnectionString))
            {
                return context.UserAliasesForTrackings.Select(u => u.Alias.ToLowerInvariant()).ToList();
            }
        }

        private List<Guid> ExecuteRule(AlertingRule rule)
        {
            List<Guid> jobIds = new List<Guid>();

            using (SqlConnection connection = new SqlConnection(_databaseConnectionString))
            {
                connection.Open();
                
                SqlCommand command = new SqlCommand(rule.SqlQuery, connection);
                command.CommandTimeout = _commandTimeout;

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    int idColumnIndex = -1;
                    try
                    {
                        idColumnIndex = reader.GetOrdinal(IdColumnName);
                    }
                    catch (IndexOutOfRangeException)
                    {
                    }

                    if (idColumnIndex < 0)
                    {
                        throw new InvalidOperationException(
                            string.Format("The sql query in rule [{0}] does not contain column[{1}]",
                                rule.SqlQuery,
                                IdColumnName));
                    }

                    while (reader.Read())
                    {
                        Guid guid = reader.GetGuid(idColumnIndex);

                        jobIds.Add(guid);
                    }
                }
            }

            return jobIds;
        }

        private bool RedirectionUrlValidationCallback(string redirectionUrl)
        {
#if DEBUG
            Console.WriteLine("Got exchange server redirection URL: {0}", redirectionUrl);
#endif
            return true;
        }

        private ExchangeService GetExchangeService(MailSettings settings)
        {
            var exchangeService = new ExchangeService();

            if (string.IsNullOrEmpty(settings.ExchangeUserPassword))
            {
                if (_getPasswordDelegate == null)
                {
                    throw new InvalidOperationException("No password is specified and no way to get password from user");
                }

                var password = _getPasswordDelegate(settings.ExchangeUserName);

                exchangeService.Credentials = new WebCredentials(new NetworkCredential(settings.ExchangeUserName, password));
            }
            else
            {
                exchangeService.Credentials = new WebCredentials(new NetworkCredential(settings.ExchangeUserName, settings.ExchangeUserPassword));
            }

            
            if (settings.AutoDiscoverUrl)
            {
                exchangeService.AutodiscoverUrl(settings.ExchangeUserName, RedirectionUrlValidationCallback);
            }
            else
            {
                exchangeService.Url = new Uri(settings.ExchangeServerUrl);
            }
            
            return exchangeService;
        }

        private void SendEmail(MailSettings mailSettings, IEnumerable<Tuple<AlertingRule, List<JobStatistic>>> jobStatistics)
        {
            // build message
            string subject = "COSMOS Job Alert";
            string body = BuildEmailBody(jobStatistics);
            var toAddresses = BuildEmailToAddress(mailSettings.ToSuffix, jobStatistics);

            EmailMessage message = new EmailMessage(GetExchangeService(mailSettings));
            
            message.From = new EmailAddress(mailSettings.From);
            //message.ToRecipients.Add(mailSettings.From);
            foreach (var addr in toAddresses)
            {
                message.ToRecipients.Add(addr);
            }

            message.Subject = subject;
            message.Body = body;
            message.Body.BodyType = BodyType.HTML;

            foreach (var addr in mailSettings.CarbonCopy)
            {
                message.CcRecipients.Add(addr);
            }

            message.Importance = Importance.High;

            message.SendAndSaveCopy();
        }

        private string BuildEmailBody(IEnumerable<Tuple<AlertingRule, List<JobStatistic>>> alertingJobs)
        {
            StringBuilder builder = new StringBuilder();

            builder.Append(@"<!DOCTYPE html>
                            <html lang=""en"" xmlns=""http://www.w3.org/1999/xhtml"">
                            <head>
                                <meta charset=""utf-8"" />
                                <title>Job details</title>
                            </head>
                            <body>");
            
            foreach (var jobs in alertingJobs)
            {
                builder.AppendFormat(@"<br /><br /><b>{0}</b>
                                        <br />
                                        <table border=""1"">
                                            <tr>
                                                <td>State</td>
                                                <td>Name</td>
                                                <td>Owner</td>
                                                <td>Used PN Hours</td>
                                                <td>Start time</td>
                                                <td>End time</td>
                                            </tr>",
                                        jobs.Item1.MailBody);

                foreach (var job in jobs.Item2)
                {
                    string jobStartTime = job.StartTime == null ? "" : string.Format("{0:u}", job.StartTime);
                    string jobEndTime = job.EndTime == null ? "" : string.Format("{0:u}", job.EndTime);

                    builder.AppendFormat(@"<tr>
                                            <td><b>{0}</b></td>
                                            <td><a href=""{1}"">{2}</a></td>
                                            <td>{3}</td>
                                            <td>{4:0.00}</td>
                                            <td>{5}</td>
                                            <td>{6}</td>
                                        </tr>",
                                          (JobInfo.JobState)job.State,
                                          job.HyperLink,
                                          job.Name,
                                          job.TrueUserName,
                                          job.PNSeconds / 3600,
                                          jobStartTime,
                                          jobEndTime);
                }

                builder.AppendFormat(@"</table>");
            }

            builder.Append(@"</body>
                            </html>");

            return builder.ToString();
        }

        private List<string> BuildEmailToAddress(string toSuffix, IEnumerable<Tuple<AlertingRule, List<JobStatistic>>> jobStatistics)
        {
            var trueUserNames = jobStatistics.SelectMany(t => t.Item2.Select(j => j.TrueUserName));
            var uniqueUserNames = trueUserNames.GroupBy(k => k).Select(g => g.Key);

            return uniqueUserNames.Select(u => u + toSuffix).ToList();
        }
    }
}
