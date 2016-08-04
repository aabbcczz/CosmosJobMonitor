using CosmosJobMonitor.Share;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VcClient;

namespace CosmosJobMonitor.CosmosJobStatistics
{
    sealed class JobFetcher
    {
        private static char[] _trueUserNameSplitChars = new char[] { '$' };
        private static char[] _domainSplitChars = new char[] { '\\' };

        private readonly string _virtualCluster;
        private readonly string _httpUrlOfVirtualCluster;
        
        private const string VirtualClusterProtocol = "vc://";

        public JobFetcher(string virtualCluster)
        {
            if (string.IsNullOrWhiteSpace(virtualCluster))
            {
                throw new ArgumentNullException("virtualCluster");
            }

            if (!virtualCluster.StartsWith(VirtualClusterProtocol))
            {
                throw new ArgumentException("virtual cluster must start with vc://");
            }

            // vc format: vc://cosmos09/relevance
            string[] fields = virtualCluster.Substring(VirtualClusterProtocol.Length).Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length != 2)
            {
                throw new ArgumentException(string.Format(@"virtual cluster [{0}] is not correct formatted", virtualCluster));
            }
            
            string cosmosClusterField = fields[0];
            string virtualClusterField = fields[1];

            _virtualCluster = virtualCluster;

            _httpUrlOfVirtualCluster = string.Format("https://{0}.osdinfra.net/cosmos/{1}", cosmosClusterField, virtualClusterField);
        }

        public void Fetch()
        {
            VC.Setup(_virtualCluster, VC.NoProxy, null);

            using (DatabaseObjectsDataContext context = new DatabaseObjectsDataContext(Properties.Settings.Default.JobStatisticsConnectionString))
            {
#if DEBUG
                context.Log = Console.Out;
#endif

                int lookbackDays = Properties.Settings.Default.LookbackDays;

                // load all data stored in database that submit time is not earlier than X days ago
                var storedJobStatistics = context.JobStatistics
                    .Where(js => js.SubmitTime != null && js.SubmitTime >= DateTime.Now.AddDays(-lookbackDays))
                    .ToDictionary(js => js.Id);

                // get jobs from server (Cosmos)
                List<JobInfo> serverJobs = new List<JobInfo>();

                var jobList = VC.GetJobsList(_virtualCluster);

                serverJobs.AddRange(jobList);

                // find out new jobs and get job statistic information for new jobs.
                var newJobs = serverJobs.Where(job => !storedJobStatistics.ContainsKey(job.ID)).ToList();

                var newJobBatches = SplitJobsToBatches(newJobs);

                int finishedNewBatchCount = 0;
                foreach (var jobBatch in newJobBatches)
                {
                    var stats = new List<JobStatistic>();

                    foreach (var job in jobBatch)
                    {
                        var statistic = GetJobStatistic(job);

                        if (statistic != null)
                        {
                            stats.Add(statistic);
                        }
                    }

                    if (stats.Any())
                    {
                        context.JobStatistics.InsertAllOnSubmit(stats);
                    }

                    context.SubmitChanges();

                    finishedNewBatchCount ++;

                    Console.WriteLine("{0}/{1} new job batches has been processed", finishedNewBatchCount, newJobBatches.Count);

                    // sleep for a while to avoid impact cosmos server too much
                    System.Threading.Thread.Sleep(5000);
                }

                // find out updated jobs and get updated job statistic information
                var updatedJobs = serverJobs.Where(job =>
                    {
                        if (!storedJobStatistics.ContainsKey(job.ID))
                        {
                            return false;
                        }

                        var storedJobStatistic = storedJobStatistics[job.ID];
                        JobInfo.JobState jobState = (JobInfo.JobState)storedJobStatistic.State;
                        if (IsJobStateFinal(jobState))
                        {
                            return false;
                        }

                        if (jobState == JobInfo.JobState.Queued && job.State == JobInfo.JobState.Queued)
                        {
                            return false;
                        }

                        return true;
                    }).ToList();

                var updatedJobBatches = SplitJobsToBatches(updatedJobs);

                int finishedUpdatingBatchCount = 0;
                foreach (var jobBatch in updatedJobBatches)
                {
                    foreach (var job in jobBatch)
                    {
                        var storedJobStatistic = storedJobStatistics[job.ID];

                        var newJobStatistic = GetJobStatistic(job);

                        if (newJobStatistic != null)
                        {
                            UpdateJobStatistic(storedJobStatistic, newJobStatistic);
                        }

                    }

                    context.SubmitChanges();

                    finishedUpdatingBatchCount++;

                    Console.WriteLine("{0}/{1} updating job batches has been processed", finishedUpdatingBatchCount, updatedJobBatches.Count);

                    // sleep for a while to avoid impact cosmos server too much
                    System.Threading.Thread.Sleep(5000);
                }
            }
        }

        private List<List<JobInfo>> SplitJobsToBatches(List<JobInfo> jobs)
        {
            int batchSize = Properties.Settings.Default.BatchJobSize;

            List<List<JobInfo>> result = new List<List<JobInfo>>();

            while (jobs.Count() > batchSize)
            {
                var batch = jobs.Take(batchSize).ToList();
                result.Add(batch);

                jobs = jobs.Skip(batchSize).ToList();
            }

            // add last batch
            result.Add(jobs);

            return result;
        }

        private void UpdateJobStatistic(JobStatistic oldStat, JobStatistic newStat)
        {
            oldStat.State = newStat.State;
            oldStat.StartTime = newStat.StartTime;
            oldStat.EndTime = newStat.EndTime;
            oldStat.SubmitTime = newStat.SubmitTime;
            oldStat.TotalRunningTimeInSecond = newStat.TotalRunningTimeInSecond;
            oldStat.PNSeconds = newStat.PNSeconds;
        }

        private string GetTrueUserName(JobInfo job)
        {
            string trueUserName = string.Empty;

            if (job.UserName.StartsWith("PHX\\", StringComparison.InvariantCultureIgnoreCase)
                && job.UserName.EndsWith("$", StringComparison.InvariantCultureIgnoreCase)
                && job.Name.ToLowerInvariant().Contains("$aether"))
            {
                // AETHER job, true user name is the "xxx" in job name "xxx$AEther..."
                var fields = job.Name.Split(_trueUserNameSplitChars);
                if (fields.Length < 2)
                {
                    Console.WriteLine("Failed to extract true user name from AETHER job name {0}", job.Name);
                }
                else
                {
                    trueUserName = fields[0];
                }
            }
            else
            {
                // remove domain if exists
                var fields = job.UserName.Split(_domainSplitChars, StringSplitOptions.RemoveEmptyEntries);
                if (fields.Length == 1)
                {
                    trueUserName = fields[0];
                }
                else if (fields.Length == 2)
                {
                    trueUserName = fields[1];
                }
                else
                {
                    Console.WriteLine("Failed to extract user name from job's user name {0}", job.UserName);
                }
            }

            return trueUserName.ToLowerInvariant();
        }

        private JobStatistic GetJobStatistic(JobInfo job)
        {
            double? pnSeconds = null;

            try
            {
                // for job that had been run less than 30 seconds, we don't count it now.
                if (job.TotalRunningTime.TotalSeconds < 1.0 
                    || (job.TotalRunningTime.TotalSeconds < 30.0 && !IsJobStateFinal(job.State)))
                {
                    return null;
                }

                var jobStatistics = VC.GetJobStatistics(job.ID.ToString());
                pnSeconds = jobStatistics.VertexStats.Take(1).Sum(v => v.TotalTimeCompleted.TotalSeconds);

                JobStatistic stat = new JobStatistic()
                {
                    Id = job.ID,
                    Name = job.Name,
                    HyperLink = string.Format("{0}/_Jobs/{1}", _httpUrlOfVirtualCluster, job.ID),
                    UserName = job.UserName,
                    TrueUserName = GetTrueUserName(job),
                    State = (int)job.State,
                    SubmitTime = job.SubmitTime,
                    StartTime = job.StartTime,
                    EndTime = job.EndTime,
                    TotalRunningTimeInSecond = (long?)job.TotalRunningTime.TotalSeconds,
                    PNSeconds = (long?)pnSeconds,
                };

                return stat;

            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to get statistics for job. ID: {0}. Exception: {1}", job.ID, ex);
                Console.WriteLine("JobInfo: {0} {1} {2} {3} {4} {5} {6}", job.Name, job.UserName, job.State, job.SubmitTime, job.StartTime, job.TotalRunningTime, pnSeconds);
            }

            return null;
        }

        private bool IsJobStateFinal(JobInfo.JobState state)
        {
            return state != JobInfo.JobState.Queued && state != JobInfo.JobState.Running;
        }
    }
}
