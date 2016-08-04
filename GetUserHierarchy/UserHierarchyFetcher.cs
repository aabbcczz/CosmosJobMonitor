using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace CosmosJobMonitor.GetUserHierarchy
{
    class UserHierarchyFetcher
    {
        private static WhoService.PeopleStoreSoapClient _client = null;

        private static WhoService.PeopleStoreSoapClient Client
        {
            get
            {
                if (_client == null)
                {
                    _client = new WhoService.PeopleStoreSoapClient("PeopleStoreSoap");
                }

                return _client;
            }
        }

        public List<string> Fetch(string topLevelUserAlias)
        {
            try
            {
                var persons = Client.GetUnder(topLevelUserAlias);

                return persons.Where(p => !string.IsNullOrWhiteSpace(p.Alias))
                    .Select(p => p.Alias)
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to get user hierarchy for {0}, exception: {1}", topLevelUserAlias, ex);
            }

            return new List<string>();
        }
    }
}
