using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Services;
using System.Web.Services;
using System.Xml.Linq;

namespace PA3WebRole
{



    /// <summary>
    /// Summary description for Admin
    /// </summary>
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
    [System.Web.Script.Services.ScriptService]
    public class Admin : System.Web.Services.WebService
    {

        // Constants
        private static string filepath = System.IO.Path.GetTempFileName();
        private const int maxSuggestions = 10;

        // Admin state
        public static bool clearingEverything = false;

        // Worker stattistics
        public static string state = "idle";
        public static string tableTotal = "0";
        public static string urlTotal = "0";
        public static string lastTenStatistic = "";

        // Set up Queue and Table Clients with the Storage Account
        private static CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                            ConfigurationManager.AppSettings["StorageConnectionString"]);
        private static CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
        private static CloudTableClient tableClient = storageAccount.CreateCloudTableClient();

        // Set up Queue and Table References
        private CloudQueue messageQueue = queueClient.GetQueueReference("mymessages");
        private CloudQueue urlQueue = queueClient.GetQueueReference("urlqueue");
        private CloudTable urlTable = tableClient.GetTableReference("myurls");
        private CloudTable statsTable = tableClient.GetTableReference("statistics");

        // Performance counters
        private PerformanceCounter cpuCounter =
            new PerformanceCounter("Processor", "% Processor Time", "_Total");
        private PerformanceCounter memCounter =
            new PerformanceCounter("Memory", "Available MBytes");

        // Cache
        private static Dictionary<string, string> cache = new Dictionary<string, string>();

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string StartCrawling()
        {

            //Create Queue Client for the message queue and puts a message into the queue 
            //in order to use the queue as an "on switch"
            CloudQueue messageQueue = queueClient.GetQueueReference("mymessages");
            messageQueue.CreateIfNotExists();


            //Create url table if it doesn't exist
            try
            {
                urlTable.CreateIfNotExists();
            }
            catch(Exception e)
            {
                return e.ToString();
            }


            // Put message in queue for worker to build from robots
            CloudQueueMessage buildRobots = new CloudQueueMessage("start crawling");
            messageQueue.AddMessage(buildRobots);

            return "It worked!";
        }

        // Sends a message to the crawler asking it to stop crawling, returns that the
        // request was sent
        [WebMethod]
        public string StopCrawling()
        {

            //Create Queue Client for the message queue and puts a message into the queue 
            //in order to use the queue as an "on switch"
            CloudQueueMessage stopCommand = new CloudQueueMessage("stop");
            messageQueue.AddMessage(stopCommand);
            string returnMessage = "Request to stop sent to crawler";

            return returnMessage;
        }

        [WebMethod]
        public string ClearIndex()
        {
            if (!clearingEverything)
            {
                StopCrawling();
                messageQueue.Clear();
                urlQueue.Clear();

                // Wait for 5 seconds to make sure the worker role recieved the message
                Thread.Sleep(5);

                ClearTables();

                return "The worker has been stopped and everything has been cleared.";
            }
            else
            {
                return "Please wait for the worker to clear!";
            };
        }

        [WebMethod]
        public void RefreshStats()
        {
            try
            {
                TableQuery<Stat> query = new TableQuery<Stat>();
                foreach (Stat statistic in statsTable.ExecuteQuery(query))
                {
                    if (statistic.Statistic.Equals("state"))
                    {
                        state = statistic.StatValue;
                    }
                    else if (statistic.Statistic.Equals("tabletotal"))
                    {
                        tableTotal = statistic.StatValue;
                    }
                    else if (statistic.Statistic.Equals("urltotal"))
                    {
                        urlTotal = statistic.StatValue;
                    }
                    else if (statistic.Statistic.Equals("lasttenstatistic"))
                    {
                        lastTenStatistic = statistic.StatValue;
                    }
                }
            }
            catch
            {
                
            }
        }

        // Clears the url table and returns whether it does or not
        [WebMethod]
        public string ClearTables()
        {
            clearingEverything = true;

            //
            statsTable.CreateIfNotExists();
            urlTable.CreateIfNotExists();

            // Delete all tables
            statsTable.Delete();
            urlTable.Delete();

            // Reset statistics to the default (When it was first made)
            state = "idling";
            tableTotal = "0";
            urlTotal = "0";
            lastTenStatistic = "";

            // Loop to ensure that the tables are created correctly
            while (true)
            {
                try
                {
                    statsTable.CreateIfNotExists();
                    urlTable.CreateIfNotExists();
                    break;
                }
                catch (StorageException e)
                {
                    if (e.RequestInformation.HttpStatusCode == 409)
                    {
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            clearingEverything = false;
            return "The tables have been cleared";
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string GetWorkerState()
        {
            RefreshStats();
            return state;
        }

        // Returns the percentage of CPU usage
        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string GetCPU()
        {
            return "" + this.cpuCounter.NextValue();
        }

        // Returns the percentage of Memory Usage
        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string GetMem()
        {
            return "" + this.memCounter.NextValue();
        }

        // Returns the number of urls in the queue
        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string GetUrlQueueSize()
        {
            // Create if doesn't exist and fetch attributes
            urlQueue.CreateIfNotExists();
            urlQueue.FetchAttributes();

            // Converts the message count to a string
            string size = "" + urlQueue.ApproximateMessageCount;

            return size;
        }

        // Returns the number of urls in the queue
        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string GetTitle(string url)
        {
            

            // Converts the message count to a string
            string size = "" + urlQueue.ApproximateMessageCount;

            return size;
        }

        // Returns the number of urls in the url table
        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string GetTableSize()
        {
            return tableTotal;
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string GetNumberUrlsCrawled()
        {
            return urlTotal;
        }

        // Returns the last 10 urls crawled
        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string GetLast10Urls()
        {
            return lastTenStatistic;
        }


        // Stops worker role, and clears queues and tables
        // Returns confirmation that everything was cleared
        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string ClearEverything()
        {
            if (!clearingEverything)
            {
                StopCrawling();
                urlQueue.Clear();
                messageQueue.Clear();

                // Sleep 5 to ensure the the worker gets the stop message
                Thread.Sleep(5000);

                ClearTables();

                return "Everything has successfully been cleared!";
            }
            else
            {
                return "Please wait until the crawler is done clearing";
            }
        }

        [WebMethod]
        public string GetPageTitle()
        {
            return "Hello World";
        }
    }
}
