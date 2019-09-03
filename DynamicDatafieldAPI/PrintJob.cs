using Amazon.DynamoDBv2.DataModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicDatafieldAPI
{
    [Serializable]
    [DynamoDBTable("identityONE_Card_PrintJobs")]
    public class PrintJob
    {
        [DynamoDBProperty]
        public String PrintJobID { get; set; }

        [DynamoDBProperty]
        public String PrintStatus { get; set; }

        [DynamoDBProperty]
        public String LayoutFrontContent { get; set; }

        [DynamoDBProperty]
        public String LayoutBackContent { get; set; }

        [DynamoDBProperty]
        public String PrintDatetime { get; set; }

    }
}
