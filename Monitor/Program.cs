using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace CosmosJobMonitor.Monitor
{
    class Program
    {
        private const string AlertingRulesFile = "AlertingRules.xml";

        private static void ShowHelpMessage()
        {
            Console.WriteLine("Monitor COSMOS job status");
            Console.WriteLine("Arguments:");
            Console.WriteLine("    -h : show this message.");
            Console.WriteLine("    -g : generate example alerting rules.");
        }

        private static SecureString GetPassword(string user)
        {
            Console.WriteLine("Please enter password for user {0}:", user);

            SecureString securePassword = new SecureString();

            ConsoleKeyInfo key;
            do
            {
                key = Console.ReadKey(true);

                // Ignore any key out of range. 
                if (((int)key.Key) >= 32 && ((int)key.Key <= 126))
                {
                    // Append the character to the password.
                    securePassword.AppendChar(key.KeyChar);
                    Console.Write("*");
                }
                // Exit if Enter key is pressed.
            } while (key.Key != ConsoleKey.Enter);

            Console.WriteLine();

            return securePassword;
        }

        static void Main(string[] args)
        {
            if (args.Where(arg => string.Compare(arg, "-h", true, System.Globalization.CultureInfo.InvariantCulture) == 0).Count() > 0)
            {
                ShowHelpMessage();
                return;
            }

            if (args.Where(arg => string.Compare(arg, "-g", true, System.Globalization.CultureInfo.InvariantCulture) == 0).Count() > 0)
            {
                GenerateExampleAlertingRules();
                return;
            }

            int exitCode = 0;
            try
            {
                CosmosJobMonitor.Share.CosmosJobMonitorUtility.SetDataDirectory(Properties.Settings.Default.DataDirectory);

                AlertingRules rules = LoadAlertingRules();

                RuleEngine engine = new RuleEngine(Properties.Settings.Default.JobStatisticsConnectionString, Properties.Settings.Default.CommandTimeout, GetPassword);

                List<Exception> exceptions;

                engine.Execute(rules, out exceptions);

                if (exceptions != null && exceptions.Any())
                {
                    Console.Error.WriteLine("There are exceptions when rule engine was executing rules:");
                    foreach(var exception in exceptions)
                    {
                        Console.Error.WriteLine("Exception: {0}", exception);
                    }

                    exitCode = -1;
                }
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

        private static AlertingRules LoadAlertingRules()
        {
            using(StreamReader reader = new StreamReader(AlertingRulesFile, Encoding.UTF8))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(AlertingRules));
                AlertingRules rules = (AlertingRules)serializer.Deserialize(reader);

                return rules;
            }
        }

        private static void GenerateExampleAlertingRules()
        {
            string exampleFile = "example." + AlertingRulesFile;

            using (StreamWriter writer = new StreamWriter(exampleFile, false, Encoding.UTF8))
            {
                var exampleAlertingRules = AlertingRules.GenerateSample();

                XmlSerializer serializer = new XmlSerializer(typeof(AlertingRules));
                serializer.Serialize(writer, exampleAlertingRules);
            }
        }
    }
}
