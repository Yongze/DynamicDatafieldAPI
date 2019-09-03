using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicDatafieldAPI
{
    public class DatabaseColumnName
    {
        [JsonProperty("person")]
        public Row[] PersonFields { get; set; }

        [JsonProperty("card")]
        public Row[] CardFields { get; set; }

        [JsonProperty("image")]
        public Image ImageFields { get; set; }

        public static DatabaseColumnName CreateSample()
        {
            DatabaseColumnName datafield = new DatabaseColumnName();
            var personFields = new List<Row>();
            personFields.Add(new Row { Name = "p.firstName" });
            personFields.Add(new Row { Name = "p.lastName" });
            personFields.Add(new Row { Name = "p.IDNumber" });
            datafield.PersonFields = personFields.ToArray();
            var cardFields = new List<Row>();
            cardFields.Add(new Row { Name = "c.barcode" });
            cardFields.Add(new Row { Name = "c.accessNumber" });
            cardFields.Add(new Row { Name = "c.expiryDate" });
            datafield.CardFields = cardFields.ToArray();
            var imageFields = new Image();
            imageFields.Height = 100;
            imageFields.Width = 100;
            datafield.ImageFields = imageFields;
            return datafield;
        }
    }

    public class Image
    {
        [JsonProperty("height")]
        public float Height { get; set; }
        [JsonProperty("width")]
        public float Width { get; set; }
    }


    public class Row
    {
        [JsonProperty("name")]
        public string Name { get; set; }

    }
}
