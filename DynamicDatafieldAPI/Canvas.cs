using Amazon.DynamoDBv2.DataModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicDatafieldAPI
{
    [Serializable]
    [DynamoDBTable("identityONE_Card_Layouts")]
    public class Canvas
    {
        [DynamoDBHashKey]
        public String ID { get; set;  }

        [DynamoDBProperty]
        public String TemplateName { get; set; }

        [DynamoDBProperty]
        public String LayoutFrontContent { get; set; }

        [DynamoDBProperty]
        public String LayoutBackContent { get; set; }

        [DynamoDBProperty]
        public String LastModifiedDatetime { get; set; }

    }
}
