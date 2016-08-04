using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CosmosJobMonitor.Share;

namespace CosmosJobMonitor.GetUserHierarchy
{
    class UserAliasesForTrackingEquityComparer : IEqualityComparer<UserAliasesForTracking>
    {
        public bool Equals(UserAliasesForTracking x, UserAliasesForTracking y)
        {
            return x.Alias == y.Alias;
        }

        public int GetHashCode(UserAliasesForTracking obj)
        {
            return obj.Alias.GetHashCode();
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            int exitCode = 0;

            try
            {
                CosmosJobMonitor.Share.CosmosJobMonitorUtility.SetDataDirectory(Properties.Settings.Default.DataDirectory);

                var aliases = GetTopLevelUserAliases();

                var allAliases = aliases.SelectMany(alias => new UserHierarchyFetcher().Fetch(alias)).ToList();

                var uniqueAliases = allAliases.GroupBy(a => a).Select(g => g.Key).ToList();

                foreach(var alias in uniqueAliases)
                {
                    Console.WriteLine("{0}", alias);
                }

                UpdateAliasesInDatabase(uniqueAliases);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Exception: {0}", ex);
                exitCode = -1;
            }

#if DEBUG
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
#endif
            Environment.Exit(exitCode);
        }

        private static string[] GetTopLevelUserAliases()
        {
            return Properties.Settings.Default.TopLevelAliases.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static void UpdateAliasesInDatabase(IEnumerable<string> uniqueAliases)
        {
            using (DatabaseObjectsDataContext context = new DatabaseObjectsDataContext(Properties.Settings.Default.JobStatisticsConnectionString))
            {
#if DEBUG
                context.Log = Console.Out;
#endif

                // find out all records to be deleted
                var uniqueAliasesDictionary = uniqueAliases.ToDictionary(a => a);

                var allAliasesToBeDeleted = context.UserAliasesForTrackings
                    .ToList()
                    .Except(uniqueAliases.Select(a => new UserAliasesForTracking() { Alias = a }), new UserAliasesForTrackingEquityComparer())
                    .ToList();

                context.UserAliasesForTrackings.DeleteAllOnSubmit(allAliasesToBeDeleted);

                // find out all records to be added
                var allAliasesToBeAdded = uniqueAliases
                    .Except(context.UserAliasesForTrackings.Select(a => a.Alias))
                    .Select(a => new UserAliasesForTracking(){ Alias = a })
                    .ToList();

                context.UserAliasesForTrackings.InsertAllOnSubmit(allAliasesToBeAdded);

                // submit changes
                context.SubmitChanges();
            }
        }
    }
}
