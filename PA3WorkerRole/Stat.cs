using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PA3WorkerRole
{
    public class Stat : TableEntity
    {
        public string Statistic { get; set; }
        public string StatValue { get; set; }

        public Stat(string stat, string statValue)
        {
            this.PartitionKey = stat;
            this.RowKey = "";

            this.Statistic = stat;
            this.StatValue = statValue;
        }

        public Stat()
        {

        }
    }
}