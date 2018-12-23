using CodeHollow.FeedReader;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace teknolot_landing_page_functions
{
    public static class RSSCrawler
    {
        [FunctionName("RSSCrawler")]
        public static async void RunAsync([TimerTrigger("0 */10 * * * *")]TimerInfo myTimer, ILogger log, ExecutionContext context)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            List<string> rssEndPoints = new List<string>
            {
                "http://daron.yondem.com/rss",
                "http://ozaksut.com/feed/",
                "http://cihanyakar.com/rss"
            };

            List<FeedItem> feedItems = new List<FeedItem>();

            var config = new ConfigurationBuilder()
              .SetBasePath(context.FunctionAppDirectory)
              .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
              .AddEnvironmentVariables()
              .Build();
            var storageConnectionString = config["AzureWebJobsStorage"];

            List<Task<Feed>> allDownloads = new List<Task<Feed>>();
            foreach (var rssEndPoint in rssEndPoints)
            {
                allDownloads.Add(FeedReader.ReadAsync(rssEndPoint));
            }
            Feed[] feeds = await Task.WhenAll(allDownloads);

            //Feeling proud!
            string FindAuthor(string Link)
            {
                if(Link.Contains("ozaksut"))
                    return "Yiğit Özaksüt";
                if (Link.Contains("daron"))
                    return "Daron Yöndem";
                if (Link.Contains("cihan"))
                    return "Cihan Yakar";
                return string.Empty;
            }

            var latestThreePosts = feeds.SelectMany(x=>x.Items).
                OrderByDescending(x => x.PublishingDate).
                Take(3).
                Select(x=> new { Author = FindAuthor(x.Link), x.Title, Url = x.Link });

            var jsonOutput = JsonConvert.SerializeObject(latestThreePosts);

            if (CloudStorageAccount.TryParse(storageConnectionString, out var storageAccount))
            {
                CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();
                var cloudBlobContainer = cloudBlobClient.GetContainerReference("json");
                CloudBlockBlob cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference("lastposts.json");
                cloudBlockBlob.Properties.ContentType = "application/json";
                await cloudBlockBlob.UploadTextAsync(jsonOutput);
            }
        }
    }
}
