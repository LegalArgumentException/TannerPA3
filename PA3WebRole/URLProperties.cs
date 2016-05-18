using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PA3WebRole
{
    public class URLProperties : TableEntity
    {
        public string URL { get; set; }
        public DateTime Date { get; set; }
        public string Extension { get; set; }

        public URLProperties()
        {

        }

        public URLProperties(string url, DateTime date)
        {
            this.PartitionKey = url.GetHashCode().ToString();
            this.RowKey = Guid.NewGuid().ToString();
            this.URL = url;
            this.Date = date;
            if (url.Contains('.'))
            {
                if (url.Contains('.') && url.Substring(url.LastIndexOf('.')).Length <= 4)
                {
                    this.Extension = url.Substring(url.LastIndexOf('.'));
                }
                else
                {
                    this.Extension = url.Substring(url.LastIndexOf('.'), url.LastIndexOf('?'));
                }
            } else
            {
                this.Extension = "No Extension";
            }
        }
    }
}