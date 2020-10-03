using System;
using System.Net;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Discord.Webhook;
using Discord;
using System.Threading.Tasks;

namespace covidCounter
{
    class Program
    {
        private static string dataUrl = "https://services1.arcgis.com/vHnIGBHHqDR6y0CR/arcgis/rest/services/COVID19_Time_Series/FeatureServer/0/query?where=1%3D1&outFields=*&outSR=4326&f=json";

        private static string envFile = ".env";

        public class dataPoint {
            public string timeStamp;
            public int newCaseCount;
            public int deathCount;
        }

        static void Main(string[] args)
        {
            Console.WriteLine("C'mon Victoria, let's get 'er done");

            Console.WriteLine("Gathering environment variables... ");
            DotNetEnv.Env.Load(envFile);
            string[] discordWebhookUrls = System.Environment.GetEnvironmentVariable("DISCORD_WEBHOOK_URLS").Split(',');

            if (discordWebhookUrls.Length == 0) {
                Console.WriteLine("Failed. Check environment variable setup. :(");
                return;
            } else {
                foreach (string discordWebhookUrl in discordWebhookUrls) {
                    Console.WriteLine(" - " + discordWebhookUrl);
                }
            }      

            Console.Write("Getting health data from Guardian Australia API... ");
            string dataString = GetData(dataUrl);
            if (dataString == String.Empty) {
                Console.WriteLine("Health API data not returned successfully or doesn't exist anymore :(");
                return;
            } else {
                Console.WriteLine("Received :)");
            }

            Console.WriteLine("Parsing data...");
            JObject dataObject = JObject.Parse(dataString);
            JToken dataFeatures = dataObject["features"];
            JToken dataAttributes;

            List<dataPoint> dataPoints = new List<dataPoint>();

            string timestamp;
            int newCaseCount;
            int deathCount;

            foreach (JToken feature in dataFeatures) {
                dataAttributes = feature.SelectToken("attributes");

                timestamp = dataAttributes.SelectToken("Date").ToString();
                Int32.TryParse(dataAttributes.SelectToken("VIC").ToString(), out newCaseCount);
                Int32.TryParse(dataAttributes.SelectToken("VIC_Deaths").ToString(), out deathCount);

                dataPoints.Add(new dataPoint {
                    timeStamp = timestamp,
                    newCaseCount = newCaseCount,
                    deathCount = deathCount
                });
            }

            dataPoint[] dataArray = dataPoints.ToArray();
            System.DateTime epochTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            DateTime dataTime;
            int index = dataArray.Length - 1;
            int newCaseFortnightAverage = 0;
            int deathFortnightAverage = 0;

            for(int i = 0; i < 14; i++) {
                newCaseCount = dataArray[index - i].newCaseCount - dataArray[index - i - 1].newCaseCount;
                newCaseFortnightAverage += newCaseCount;
                deathCount = dataArray[index - i].deathCount - dataArray[index - i - 1].deathCount;
                deathFortnightAverage += deathCount;

                dataTime = epochTime.AddMilliseconds(Convert.ToDouble(dataArray[index].timeStamp)).ToUniversalTime();
            }

            newCaseFortnightAverage = newCaseFortnightAverage / 14;
            deathFortnightAverage = deathFortnightAverage / 14;
            string endDate = epochTime.AddMilliseconds(Convert.ToDouble(dataArray[index].timeStamp)).ToUniversalTime().ToString("dddd, dd MMMM yyyy");
            string startDate = epochTime.AddMilliseconds(Convert.ToDouble(dataArray[index - 13].timeStamp)).ToUniversalTime().ToString("dddd, dd MMMM yyyy");

            Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");
            Console.WriteLine("Fortnightly averages for Covid-19 for Victoria, Australia");
            Console.WriteLine("Between " + startDate + " and " + endDate );
            Console.WriteLine("Average new cases: " + newCaseFortnightAverage);
            Console.WriteLine("Average deaths: " + deathFortnightAverage);
            Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");

            /*  Todo:
            *   - Add graph of averages with today's daily reading
            *   - Add celebrations for Melbournes reopening steps average tiers (5, 0)
            */

            AnnounceToDiscord(discordWebhookUrls, newCaseFortnightAverage, deathFortnightAverage, startDate, endDate);
        }

        public static string GetData(string url)
        {
            var client = new WebClient();
            var response = client.DownloadString(url);
            return response;
        }

        public static void AnnounceToDiscord(string[] discordWebhookUrls, int newCaseFortnightAverage, int deathFortnightAverage, string startDate, string endDate) 
        {
            Console.WriteLine("Announcing to " + discordWebhookUrls.Length + " Discord channels");

            var embed = ConstructEmbed(newCaseFortnightAverage, deathFortnightAverage, startDate, endDate);

            for (int i = 0; i < discordWebhookUrls.Length; i++) {
                Console.WriteLine(discordWebhookUrls[i]);
                using (var client = new DiscordWebhookClient(discordWebhookUrls[i]))
                {
                    Console.Write("Attempting to announce...");

                    // Webhooks are able to send multiple embeds per message
                    // As such, your embeds must be passed as a collection.
                    Task<ulong> sendMessage = Task.Run(() => client.SendMessageAsync(embeds: new[] { embed }));
                    // Wait for it to finish
                    sendMessage.Wait();
                    // Get the result
                    var result = sendMessage.Result;

                    if (result != 0) Console.WriteLine("Success! Comment ID: " + result);
                    else Console.WriteLine("Failure. :( Check Webhook URL");
                }
            }
        }

        public static Embed ConstructEmbed(int newCaseFortnightAverage, int deathFortnightAverage, string startDate, string endDate) 
        {
            Console.Write("Constructing Discord embed... ");

            var author = new EmbedAuthorBuilder()
            .WithName("Melboune Covid-19 tracker by 🦎 Lizardman")
            .WithUrl("https://github.com/101Lizardman/MelbourneDiscordCovidTracker");

            var footer = new EmbedFooterBuilder()
            .WithText("Don't be a dumbass 🤡, wear a mask 😷");

            var emptySpace = new EmbedFieldBuilder()
            .WithName("\u200B")
            .WithValue("\u200B");

            var newCasesField = new EmbedFieldBuilder()
            .WithName("Average new cases:")
            .WithValue("`" + newCaseFortnightAverage + "`")
            .WithIsInline(true);

            var deathsField = new EmbedFieldBuilder()
            .WithName("Average deaths:")
            .WithValue("`" + deathFortnightAverage + "`")
            .WithIsInline(true);

            var dataField = new EmbedFieldBuilder()
            .WithName("📉 Data gathered from")
            .WithValue("[Guardian Australia](https://covid19-esriau.hub.arcgis.com/datasets/current-cases-deaths-tests-by-state)");

            var reopeningField = new EmbedFieldBuilder()
            .WithName("Checkout")
            .WithValue("[Melbourne's reopening steps](https://www.coronavirus.vic.gov.au/coronavirus-covid-19-reopening-roadmap-metro-melbourne)");
            
            List<EmbedFieldBuilder> fields = new List<EmbedFieldBuilder>() {
                emptySpace,
                newCasesField,
                deathsField,
                emptySpace,
                dataField,
                reopeningField
            };

            var embed = new EmbedBuilder()
            .WithTitle("Fortnightly averages for Covid-19 for Victoria, Australia")
            .WithDescription("Between `" + startDate + "` and `" + endDate + "`")
            .WithAuthor(author)
            .WithFields(fields)
            .WithFooter(footer)
            .WithCurrentTimestamp()
            .Build();

            if (embed != null) Console.WriteLine("Success :)");
            return embed;
        }
    }
}
