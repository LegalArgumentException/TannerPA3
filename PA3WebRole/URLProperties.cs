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
        public string Date { get; set; }
        public string Extension { get; set; }

        public URLProperties(string url, string date)
        {
            this.PartitionKey = url;
            this.RowKey = Guid.NewGuid().ToString();
            this.URL = url;
            this.Date = date;
            if (url.Substring(url.LastIndexOf('.')).Length <= 4) {
                this.Extension = url.Substring(url.LastIndexOf('.'));
            } else
            {
                this.Extension = url.Substring(url.LastIndexOf('.'), url.LastIndexOf('?'));
            }
        }
    }
}