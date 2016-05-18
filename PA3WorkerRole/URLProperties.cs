using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PA3WorkerRole
{
    public class URLProperties : TableEntity
    {
        public string URL { get; set; }
        public DateTime Date { get; set; }
        public string Extension { get; set; }
        public string Title { get; set; }

        public URLProperties(string url, DateTime date, string title)
        {
            this.PartitionKey = url.GetHashCode().ToString();
            this.RowKey = Guid.NewGuid().ToString();
            this.URL = url;
            this.Date = date;
            this.Title = title;
            if (url.Contains('.') && (url.Length - url.LastIndexOf('.') < 5))
            {
                this.Extension = url.Substring(url.LastIndexOf('.'));
            }
            else if (url.Contains('?'))
            {
                this.Extension = url.Substring(url.LastIndexOf('.'), url.LastIndexOf('?'));
            }
            else
            {
                this.Extension = "No Extension";
            }
        }

        public URLProperties(string url, DateTime date)
        {
            this.PartitionKey = url.GetHashCode().ToString();
            this.RowKey = Guid.NewGuid().ToString();
            this.URL = url;
            this.Date = date;
            this.Title = "";
            if (url.Contains('.') && (url.Length - url.LastIndexOf('.') < 5))
            {
                this.Extension = url.Substring(url.LastIndexOf('.'));
            } else if(url.Contains('?'))
            {
                this.Extension = url.Substring(url.LastIndexOf('.'), url.LastIndexOf('?'));
            } else
            {
                this.Extension = "No Extension";
            }
        }

        public URLProperties(string url)
        {
            this.PartitionKey = url.GetHashCode().ToString();
            this.RowKey = Guid.NewGuid().ToString();
            this.URL = url;
            this.Date = DateTime.Now;
            this.Title = "";
            if (url.Contains('.') && (url.Length - url.LastIndexOf('.') < 5))
            {
                this.Extension = url.Substring(url.LastIndexOf('.'));
            }
            else if (url.Contains('?'))
            {
                this.Extension = url.Substring(url.LastIndexOf('.'), url.LastIndexOf('?'));
            }
            else
            {
                this.Extension = "No Extension";
            }
        }

        public URLProperties()
        {
            this.PartitionKey = "";
            this.RowKey = "";
            this.URL = "";
            this.Date = DateTime.Now;
            this.Extension = "";
            this.Title = "";
        }
    }
}