using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Newtonsoft.Json;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using System.IO;
using Amazon.DynamoDBv2.Model;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace DynamicDatafieldAPI
{
    public class Functions
    {
        /// <summary>
        /// Default constructor that Lambda will invoke.
        /// </summary>
        public Functions()
        {
        }

        public APIGatewayProxyResponse Get(APIGatewayProxyRequest request, ILambdaContext context)
        {
            DatabaseColumnName fields = DatabaseColumnName.CreateSample();
            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = JsonConvert.SerializeObject(fields),
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" }, { "Access-Control-Allow-Origin", "*" } }
            };
            return response;
        }
        private static void PrintItem(Dictionary<string, AttributeValue> attributeList, ILambdaContext context)
        {
            foreach (KeyValuePair<string, AttributeValue> kvp in attributeList)
            {
                string attributeName = kvp.Key;
                AttributeValue value = kvp.Value;

                context.Logger.Log(
                    attributeName + " " +
                    (value.S == null ? "" : "S=[" + value.S + "]") +
                    (value.N == null ? "" : "N=[" + value.N + "]") +
                    (value.SS == null ? "" : "SS=[" + string.Join(",", value.SS.ToArray()) + "]") +
                    (value.NS == null ? "" : "NS=[" + string.Join(",", value.NS.ToArray()) + "]")
                    );
            }
            context.Logger.Log("************************************************");
        }
        public async Task<APIGatewayProxyResponse> Save(APIGatewayProxyRequest request, ILambdaContext context)
        {
            JSONObject json = JsonConvert.DeserializeObject<JSONObject>(request?.Body);
            DynamoDBContext dbContext = new DynamoDBContext(new AmazonDynamoDBClient(RegionEndpoint.APSoutheast2));
            try
            {
                AmazonDynamoDBClient client = new AmazonDynamoDBClient(RegionEndpoint.APSoutheast2);

                //get files' name
                var req = new ScanRequest
                {
                    TableName = "identityONE_Card_Layouts",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
                        {":val", new AttributeValue {
                             S = json.Name
                         }}
                    },
                    FilterExpression = "TemplateName = :val",
                    ProjectionExpression = "ID, LayoutFrontContent, LayoutBackContent",
                };
                var resp = await client.ScanAsync(req);

                foreach (Dictionary<string, AttributeValue> item in resp.Items)
                {
                    //delete files' name
                    await DeleteS3Object(item["LayoutFrontContent"].S, context);
                    await DeleteS3Object(item["LayoutBackContent"].S, context);
                    
                    //delete dynamoDB record
                    await dbContext.DeleteAsync<Canvas>(item["ID"].S);
                }

                //var response = new APIGatewayProxyResponse
                //{
                //    StatusCode = (int)HttpStatusCode.OK,
                //    Body = JsonConvert.SerializeObject(1),
                //    Headers = new Dictionary<string, string> { { "Content-Type", "application/json" }, { "Access-Control-Allow-Origin", "*" } }
                //};
                //return response;
            }
            catch (Exception ex)
            {
                return ReturnResponse(ex);
            }

            // insert a new record
            String id = json.ID == null ? Guid.NewGuid().ToString() : json.ID;
            Canvas canvas = new Canvas
            {
                ID = id,
                TemplateName = json.Name,
                LayoutFrontContent = JsonConvert.SerializeObject(json.Front),
                LayoutBackContent = JsonConvert.SerializeObject(json.Back),
                LastModifiedDatetime = DateTime.Now.ToString("yyyy/MM/dd hh:mm:ss tt"),
            };
            if (await PutS3Object("id1.carddesign.templates", canvas.ID + "_front.txt", canvas.LayoutFrontContent, context)
                && await PutS3Object("id1.carddesign.templates", canvas.ID + "_back.txt", canvas.LayoutBackContent, context))
            {
                string front = canvas.ID + "_front.txt";
                canvas.LayoutFrontContent = front;
                string back = canvas.ID + "_back.txt";
                canvas.LayoutBackContent = back;
                try
                {
                    await dbContext.SaveAsync<Canvas>(canvas);
                }
                catch (Exception ex)
                {
                    return ReturnResponse(ex);
                }
            }

            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = JsonConvert.SerializeObject(id),
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" }, { "Access-Control-Allow-Origin", "*" } }
            };
            return response;
        }
        public async Task<APIGatewayProxyResponse> GetLayoutContentByID(APIGatewayProxyRequest request, ILambdaContext context)
        {
            string[] fileNames = JsonConvert.DeserializeObject<string[]>(request?.Body);

            List<string> content = new List<string>();

            content.Add(await GetS3Object(fileNames[0], context));
            content.Add(await GetS3Object(fileNames[1], context));

            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = JsonConvert.SerializeObject(content),
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" }, { "Access-Control-Allow-Origin", "*" } }
            };
            return response;
        }


        public async Task<APIGatewayProxyResponse> Print(APIGatewayProxyRequest request, ILambdaContext context)
        {
            JSONObject json = JsonConvert.DeserializeObject<JSONObject>(request?.Body);
            String id = Guid.NewGuid().ToString();

            DynamoDBContext dbContext = new DynamoDBContext(new AmazonDynamoDBClient(RegionEndpoint.APSoutheast2));
            PrintJob job = new PrintJob
            {
                PrintJobID = id,
                PrintStatus = "Pending",
                LayoutFrontContent = JsonConvert.SerializeObject(json.Front),
                LayoutBackContent = JsonConvert.SerializeObject(json.Back),
                PrintDatetime = DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString(),
            };
            if (await PutS3Object("id1.carddesign.printjobs", job.PrintJobID + "_front.txt", job.LayoutFrontContent, context)
                && await PutS3Object("id1.carddesign.printjobs", job.PrintJobID + "_back.txt", job.LayoutBackContent, context))
            {
                string front = job.PrintJobID + "_front.txt";
                job.LayoutFrontContent = front;
                string back = job.PrintJobID + "_back.txt";
                job.LayoutBackContent = back;
                try
                {
                    await dbContext.SaveAsync<PrintJob>(job);
                }
                catch (Exception ex)
                {
                    context.Logger.Log(ex.Message);
                    return ReturnResponse(ex);
                }
            }

            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = JsonConvert.SerializeObject("OK"),
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" }, { "Access-Control-Allow-Origin", "*" } }
            };
            return response;
        }

        public async Task<APIGatewayProxyResponse> isExisting(APIGatewayProxyRequest request, ILambdaContext context)
        {
            string name = JsonConvert.DeserializeObject<string>(request?.Body);
            DynamoDBContext dbContext = new DynamoDBContext(new AmazonDynamoDBClient(RegionEndpoint.APSoutheast2));
            try
            {
                AmazonDynamoDBClient client = new AmazonDynamoDBClient(RegionEndpoint.APSoutheast2);
                var req = new ScanRequest
                {
                    TableName = "identityONE_Card_Layouts",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
                        {":val", new AttributeValue {
                             S = name
                         }}
                    },
                    FilterExpression = "TemplateName = :val",
                    ProjectionExpression = "ID",
                };
                var resp = await client.ScanAsync(req);

                var response = new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.OK,
                    Body = JsonConvert.SerializeObject(resp.Items.Count > 0),
                    Headers = new Dictionary<string, string> { { "Content-Type", "application/json" }, { "Access-Control-Allow-Origin", "*" } }
                };
                return response;
            }
            catch (Exception ex)
            {
                return ReturnResponse(ex);
            }
        }

        public async Task<APIGatewayProxyResponse> Delete(APIGatewayProxyRequest request, ILambdaContext context)
        {
            //System.Threading.Thread.Sleep(5000);
            DynamoDBContext dbContext = new DynamoDBContext(new AmazonDynamoDBClient(RegionEndpoint.APSoutheast2));
            try
            {
                //delete in s3
                string[] filenames = JsonConvert.DeserializeObject<string[]>(request?.Body);

                bool d1 = await DeleteS3Object(filenames[1], context);
                bool d2 = await DeleteS3Object(filenames[2], context);

                //delete in dynamo db
                if(d1 && d2)
                {
                    await dbContext.DeleteAsync<Canvas>(filenames[0]);
                }

                var response = new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.OK,
                    Body = JsonConvert.SerializeObject(d1 && d2),
                    Headers = new Dictionary<string, string> { { "Content-Type", "application/json" }, { "Access-Control-Allow-Origin", "*" } }
                };
                return response;
            }
            catch (Exception ex)
            {
                return ReturnResponse(ex);
            }
        }

        public static APIGatewayProxyResponse ReturnResponse(Exception ex)
        {
            return new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.InternalServerError,
                Body = JsonConvert.SerializeObject(ex),
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
            };
        }

        public async Task<APIGatewayProxyResponse> Read(APIGatewayProxyRequest request, ILambdaContext context)
        {
            var response = new APIGatewayProxyResponse();
            try
            {
                DynamoDBContext dbContext = new DynamoDBContext(new AmazonDynamoDBClient(RegionEndpoint.APSoutheast2));
                AsyncSearch<Canvas> search = dbContext.ScanAsync<Canvas>(Enumerable.Empty<ScanCondition>(), null);
                List<Canvas> result = await search.GetRemainingAsync();
                //List<Canvas> list = new List<Canvas>();

                //foreach (Canvas item in result)
                //{
                //    Canvas canvas = item;
                //    string back = await GetS3Object(item.LayoutBackContent, context);
                //    canvas.LayoutBackContent = back;
                //    string front = await GetS3Object(item.LayoutFrontContent, context);
                //    canvas.LayoutFrontContent = front;
                //    list.Add(canvas);
                //}
                //context.Logger.Log(JsonConvert.SerializeObject(result));


                //AmazonDynamoDBClient client = new AmazonDynamoDBClient(RegionEndpoint.APSoutheast2);

                //var req = new ScanRequest
                //{
                //    TableName = "identityONE_Card_Layouts",
                //    ProjectionExpression = "ID, Name, LastModifiedDatetime"
                //};

                //var resp = await client.ScanAsync(req);

                //foreach (Dictionary<string, AttributeValue> item in resp.Items)
                //{
                //    // Process the result.
                //    context.Logger.Log(JsonConvert.SerializeObject(item));
                //}

                response.StatusCode = (int)HttpStatusCode.OK;
                response.Body = JsonConvert.SerializeObject(result);
                response.Headers = new Dictionary<string, string> { { "Content-Type", "application/json" }, { "Access-Control-Allow-Origin", "*" } };
            }
            catch (Exception ex)
            {
                return ReturnResponse(ex);
            }

            return response;
        }

        public async Task<bool> PutS3Object(string bucket, string key, string content, ILambdaContext context)
        {
            try
            {
                using (var client = new AmazonS3Client(RegionEndpoint.APSoutheast2))
                {
                    var request = new PutObjectRequest
                    {
                        BucketName = bucket, // bucket name
                        Key = key, // key name or file name
                        ContentBody = content // String content
                    };
                    var response = await client.PutObjectAsync(request);

                }

                return true;
            }
            catch (Exception ex)
            {
                context.Logger.Log("Exception in PutS3Object: " + ex.Message);
                return false;
            }
        }

        public async Task<string> GetS3Object(string key, ILambdaContext context)
        {
            string bucket = "id1.carddesign.templates";
            var client = new AmazonS3Client(RegionEndpoint.APSoutheast2);
            string responseBody = "";
            try
            {
                GetObjectRequest request = new GetObjectRequest
                {
                    BucketName = bucket,
                    Key = key
                };
                using (GetObjectResponse response = await client.GetObjectAsync(request))
                using (Stream responseStream = response.ResponseStream)
                using (StreamReader reader = new StreamReader(responseStream))
                {

                    responseBody = reader.ReadToEnd(); // Now you process the response body.
                    //context.Logger.Log("BODY: " + responseBody);
                    return responseBody;
                }
            }
            catch (AmazonS3Exception e)
            {
                Console.WriteLine("Error encountered ***. Message:'{0}' when writing an object", e);
                return null;
            }
            catch (Exception e)
            {
                Console.WriteLine("Unknown encountered on server. Message:'{0}' when writing an object", e);

                return null;
            }
        }

        public async Task<bool> DeleteS3Object(string key, ILambdaContext context)
        {
            string bucket = "id1.carddesign.templates";
            var client = new AmazonS3Client(RegionEndpoint.APSoutheast2);
            try
            {
                DeleteObjectRequest request = new DeleteObjectRequest
                {
                    BucketName = bucket,
                    Key = key
                };

                // Issue request
                await client.DeleteObjectAsync(request);
                return true;
            }
            catch (AmazonS3Exception e)
            {
                Console.WriteLine("Error encountered ***. Message:'{0}' when writing an object", e);
            }
            catch (Exception e)
            {
                Console.WriteLine("Unknown encountered on server. Message:'{0}' when writing an object", e);
            }
            return false;
        }
    }
}
