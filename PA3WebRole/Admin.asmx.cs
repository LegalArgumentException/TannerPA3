using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
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
    // [System.Web.Script.Services.ScriptService]
    public class Admin : System.Web.Services.WebService
    {
        private CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                    ConfigurationManager.AppSettings["StorageConnectionString"]
                );

        [WebMethod]
        public List<string> StartCrawling()
        {
            //Actual Web Method

            //Create Queue Client for the message queue and puts a message into the queue 
            //in order to use the queue as an "on switch"
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
            CloudQueue messageQueue = queueClient.GetQueueReference("mymessages");
            messageQueue.CreateIfNotExists();

            // Test Message
            CloudQueueMessage onMessage = new CloudQueueMessage("Crawler On");
            messageQueue.Clear();
            messageQueue.AddMessage(onMessage);

            
            //Create url table if it doesn't exist

            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable urlTable = tableClient.GetTableReference("myurls");
            try
            {
                urlTable.CreateIfNotExists();
            }
            catch(Exception e)
            {
                List<string> exceptionList = new List<string>();
                exceptionList.Add(e.ToString());
                return exceptionList;
            }

            
            // Stuff for Worker Role
            List<string> returnString;
            returnString = ParseRobotsTxt("http://www.cnn.com/robots.txt");
            return returnString;
        }

        [WebMethod]
        public string StopCrawling()
        {
            //Create Storage Account Client
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                    ConfigurationManager.AppSettings["StorageConnectionString"]
                );

            //Create Queue Client for the message queue and puts a message into the queue 
            //in order to use the queue as an "on switch"
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
            CloudQueue messageQueue = queueClient.GetQueueReference("mymessages");
            messageQueue.Clear();

            return "works";
        }

        [WebMethod]
        public string ClearIndex()
        {
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable urlTable = tableClient.GetTableReference("myurls");
            if (urlTable.Exists())
            {
                urlTable.Delete();
            }
            return "works";
        }

        [WebMethod]
        public string GetPageTitle()
        {
            return "Hello World";
        }

        private List<string> ParseRobotsTxt(string url)
        {
            string urlAddress = url;

            List<string> data = new List<string>();

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(urlAddress);
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
                while((streamLine = readStream.ReadLine()) != null)
                {
                    if (streamLine.StartsWith("Sitemap: "))
                    {
                        data.Add(streamLine.Substring(8));
                    }
                }

                foreach(string xml in data)
                {
                    parseXML(xml);
                }

                response.Close();
                readStream.Close();
            }
            return data;
        }

        private void parseXML(string xml)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(xml);
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();

            // Check to see if it's a valid URL
            if (response.StatusCode == HttpStatusCode.OK)
            {
                XDocument xdoc = XDocument.Load(xml);

            }
        }
    }
}
