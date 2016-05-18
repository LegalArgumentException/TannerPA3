using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Configuration;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using System.Xml.Linq;
using Microsoft.WindowsAzure.Storage.Table;
using System.IO;
using System.Text;
using HtmlAgilityPack;

namespace PA3WorkerRole
{

    public class WorkerRole : RoleEntryPoint
    {

        // Worker Role State
        private static string state = "idling";
        private static bool loadedRobots = false;

        // Storage Account Reference
        private static CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                    ConfigurationManager.AppSettings["StorageConnectionString"]
                );

        // Queue Set-up
        private static CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
        private static CloudQueue messageQueue = queueClient.GetQueueReference("mymessages");
        private static CloudQueue urlQueue = queueClient.GetQueueReference("urlqueue");

        // Table Set-up
        private static CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
        private static CloudTable urlTable = tableClient.GetTableReference("myurls");
        private static CloudTable statisticsTable = tableClient.GetTableReference("statistics");

        // Set of all crawled URL's
        private static HashSet<string> crawledUrls = new HashSet<string>();
        private static int repeats = 0;

        // Statistics
        private static int urlTotal = 0;
        private static int tableTotal = 0;
        private static List<string> errors = new List<string>();
        private static Queue<string> lastTenCrawled = new Queue<string>();

        // Disallows for robots.txt (don't wanna get blacklisted!)
        private static List<string> disallowList = new List<string>();

        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly ManualResetEvent runCompleteEvent = new ManualResetEvent(false);

        public override void Run()
        {
            Trace.TraceInformation("PA3WorkerRole is running");

            while (true)
            {
                
                Thread.Sleep(50); // Pause for 50 ms

                try
                {
                    // Create the queue if it doesn't exist
                    messageQueue.CreateIfNotExists();

                    // Create table if it doesn't exist

                    // Get message and remove it from the queue
                    CloudQueueMessage webRoleMessage = messageQueue.GetMessage();
                    messageQueue.DeleteMessage(webRoleMessage);

                    // Handle the message sent by the Web Roll
                    if (webRoleMessage.AsString == "start crawling")
                    {
                        state = "building";
                        UpdateState();
                    }
                    else if (webRoleMessage.AsString == "stop")
                    {
                        state = "stopped";
                        loadedRobots = false;
                        UpdateState();
                    }
                }
                catch (Exception e)
                {
                }

                UpdateState();

                if (state == "stopped")
                {
                    // Do nothing - the worker role is waiting for input
                }
                else if (state == "building")
                {
                    if (!loadedRobots)
                    {
                        urlQueue.CreateIfNotExists();
                        ParseRobotsTxt("http://www.cnn.com/robots.txt");

                    }

                    state = "crawling urls";
                    UpdateState();

                }
                else if (state == "crawling urls")
                {
                    urlTable.CreateIfNotExists();
                    CrawlUrls();
                }
            }
            
        }

        public override bool OnStart()
        {
            // Set the maximum number of concurrent connections
            ServicePointManager.DefaultConnectionLimit = 12;

            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.

            bool result = base.OnStart();

            Trace.TraceInformation("PA3WorkerRole has been started");
            
            return result;
        }

        public override void OnStop()
        {
            Trace.TraceInformation("PA3WorkerRole is stopping");

            this.cancellationTokenSource.Cancel();
            this.runCompleteEvent.WaitOne();

            base.OnStop();

            Trace.TraceInformation("PA3WorkerRole has stopped");
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
           
        }

        private void ParseRobotsTxt(string url)
        {

            List<string> data = new List<string>();

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();

            if (response.StatusCode == HttpStatusCode.OK)
            {
                Stream receiveStream = response.GetResponseStream();
                StreamReader readStream = null;

                if (response.CharacterSet == null)
                {
                    readStream = new StreamReader(receiveStream);
                }
                else
                {
                    readStream = new StreamReader(receiveStream, Encoding.GetEncoding(response.CharacterSet));
                }

                string streamLine;
                while ((streamLine = readStream.ReadLine()) != null)
                {
                    if (streamLine.StartsWith("Sitemap: "))
                    {
                        data.Add(streamLine.Substring(9));
                    }

                    if (streamLine.StartsWith("Disallow: "))
                    {
                        disallowList.Add(streamLine.Substring(9));
                    }
                }

                response.Close();
                readStream.Close();

                foreach(string xml in data)
                {
                    parseXML(xml);
                }
            }
            loadedRobots = true;
        }

        private void parseXML(string xml)
        {

            //Parse XML of page
            XDocument xdoc = XDocument.Load(xml);
            XNamespace df = xdoc.Root.Name.Namespace;
            bool hasDate = true;

            // Boolean to see if parsing more xml or html pages
            bool xmlUrls = (xdoc.Descendants(df + "sitemap").Count() > 0);
            List<URLProperties> elements;

            if (xmlUrls)
            {
                // Get list of xml elements
                elements =
                     (from url in xdoc.Descendants(df + "sitemap")
                      select new URLProperties(url.Element(df + "loc").Value, Convert.ToDateTime(url.Element(df + "lastmod").Value))).ToList();
            }
            else
            {

                // Get list of html elements
                if (xdoc.Descendants(df + "url").First().Element(df + "lastmod") != null)
                {
                    elements =
                    (from url in xdoc.Descendants(df + "url")
                     select new URLProperties(url.Element(df + "loc").Value, Convert.ToDateTime(url.Element(df + "lastmod").Value))).ToList();
                }
                else
                {
                    elements =
                    (from url in xdoc.Descendants(df + "url")
                     select new URLProperties(url.Element(df + "loc").Value)).ToList();
                }
            }


            // Loop through each element found and add each to table and queue
            foreach (URLProperties url in elements)
            {
                string urlString = url.URL;

                if (urlString.EndsWith("/") && !urlString.Contains(".htm")) {
                    urlString = urlString + "index.html";
                }
                if (!urlString.EndsWith(".xml") && !urlString.Contains(".htm"))
                {
                    urlString = urlString + "/index.html";
                }

                if(crawledUrls.Contains(urlString))
                {
                    repeats++;
                    return;
                }

                crawledUrls.Add(urlString);

                if (hasDate)
                {
                    DateTime now = DateTime.Now;
                    DateTime other = url.Date;
                }
                if (!hasDate || (DateTime.Now - url.Date).TotalDays < 62)
                {
                    if (url.URL.EndsWith(".xml"))
                    {
                        // Url is to a sitemap, parse through recursively
                        parseXML(urlString);
                    }
                    else
                    {
                        
                        // Url is to an html page, add to queue for crawling after completion
                        CloudQueueMessage html = new CloudQueueMessage(urlString);
                        urlQueue.AddMessage(html);
                    }
                }
            }

        }

        private void CrawlUrls()
        {
            try
            {
                CloudQueueMessage urlQueueMessage = urlQueue.GetMessage();
                urlQueue.DeleteMessage(urlQueueMessage);
                string originalUrl = urlQueueMessage.AsString;

                // Add Url to list of crawled Urls
                crawledUrls.Add(originalUrl);

                // Load html of current url
                HtmlWeb web = new HtmlWeb();
                HtmlDocument currentPage = web.Load(originalUrl);

                // Gets the root of the current url so it can be appended to any url found that uses
                // a local path
                string originalRoot;

                if (originalUrl.Contains("bleacherreport"))
                {
                    originalRoot = originalUrl.Substring(0, originalUrl.IndexOf("bleacherreport.com") + 18);
                }
                else
                {
                    originalRoot = originalUrl.Substring(0, originalUrl.IndexOf("cnn.com") + 7);
                }

                string date = "Unspecified";

                // Get the date the page was made (if applicable)
                HtmlNode dateNode = currentPage.DocumentNode.SelectSingleNode("//meta[@name='pubdate']");

                // Get title of page
                string title = currentPage.DocumentNode.SelectSingleNode("//title").InnerHtml;

                if (dateNode != null)
                {
                    date = dateNode.Attributes["content"].Value;
                }

                // Insert in Table
                URLProperties pageInfo = new URLProperties(originalUrl, Convert.ToDateTime(date), title);
                TableOperation insertOperation = TableOperation.Insert(pageInfo);
                urlTable.Execute(insertOperation);
                UpdateTableSize();

                // Incriment URL Count
                UpdateUrlCounts();
                UpdateLast10Urls(originalUrl);

                // Get all hrefs in page
                var allHrefs = currentPage.DocumentNode.SelectNodes("//a[@href]");

                // Loop through the value of each href found
                foreach (HtmlNode url in allHrefs)
                {
                    string urlValue = url.Attributes["href"].Value;

                    // If the value of the href is a local, add the root to the beginning
                    if (urlValue.StartsWith("/"))
                    {
                        urlValue = originalRoot + urlValue;
                    }
                    if (urlValue.EndsWith("/"))
                    {
                        urlValue = urlValue + "index.html";
                    }
                    if (!urlValue.EndsWith(".html") && !urlValue.EndsWith(".htm"))
                    {
                        urlValue = urlValue + "/index.html";
                    }

                    // Check to see if the page has already been put in the list
                        if (!crawledUrls.Contains(urlValue))
                    {
                        // Check to see if the page is disallowed
                        foreach (string disallowed in disallowList)
                        {
                            if (urlValue.Contains(disallowed))
                            {
                                return;
                            }
                        }

                        // Weed out all href errors and add to table
                        if (urlValue.EndsWith(".html"))
                        {
                            CloudQueueMessage newUrl = new CloudQueueMessage(urlValue);
                            urlQueue.AddMessage(newUrl);
                        }
                    }   

                }
            }
            catch (Exception e)
            {

            }
        }

        // All statistics table operations

        // Updates the state in the statistics table
        public void UpdateState()
        {
            try
            {
                statisticsTable.CreateIfNotExists();
                Stat stateStatistic = new Stat("state", state);
                // Replace state in table
                TableOperation replace = TableOperation.InsertOrReplace(stateStatistic);
                statisticsTable.Execute(replace);
            }
            catch
            {
                
            }
        }

        // Change table size in stats table
        public void UpdateTableSize()
        {
            try
            {
                tableTotal++;
                statisticsTable.CreateIfNotExists();
                Stat tableTotalStatistic = new Stat("tabletotal", tableTotal.ToString());
                TableOperation replace = TableOperation.InsertOrReplace(tableTotalStatistic);
                statisticsTable.Execute(replace);
            }
            catch { }
        }

        // Change url count in stats table
        public void UpdateUrlCounts()
        {
            try
            {
                urlTotal++;
                statisticsTable.CreateIfNotExists();
                Stat urlTotalStatistic = new Stat("urltotal", urlTotal.ToString());
                TableOperation replace = TableOperation.InsertOrReplace(urlTotalStatistic);
                statisticsTable.Execute(replace);
            }
            catch { }
        }

        // Change last 10 urls in stats table
        public void UpdateLast10Urls(string url)
        {
            try
            {
                lastTenCrawled.Enqueue(url);
                while (lastTenCrawled.Count > 10)
                {
                    lastTenCrawled.Dequeue();
                }

                string list = string.Join("  /  ", lastTenCrawled.ToArray());

                statisticsTable.CreateIfNotExists();
                Stat lastTenStatistic = new Stat("lasttenstatistic", list);
                TableOperation replace = TableOperation.InsertOrReplace(lastTenStatistic);
                statisticsTable.Execute(replace);
            }
            catch { }
        }
    }
}
