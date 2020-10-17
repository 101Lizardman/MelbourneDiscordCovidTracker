using System;
using System.Net;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Discord.Webhook;
using Discord;
using System.Threading.Tasks;
using System.Text;
using ChoETL;

namespace covidCounter
{
    class Program
    {
        private static string dataUrl = "https://raw.githubusercontent.com/M3IT/COVID-19_Data/master/Data/COVID_AU_state.csv";

        private static string envFile = ".dev.env";

        public class dataPoint {
            public DateTime dateTime;
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
            Dictionary<string, string> data = ParseData(dataString);

            Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");
            Console.WriteLine("Fortnightly averages for Covid-19 for Victoria, Australia");
            Console.WriteLine("Between " + data["startDate"] + " and " + data["endDate"] );
            Console.WriteLine("New cases today: " + data["newCasesToday"] );
            Console.WriteLine("Average new cases: " + data["newCaseFortnightAverage"]);
            Console.WriteLine("Average deaths: " + data["deathFortnightAverage"]);
            Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");

            /*  Todo:
            *   - Add graph of averages with today's daily reading
            *   - Add celebrations for Melbournes reopening steps average tiers (5, 0)
            */

            AnnounceToDiscord(discordWebhookUrls, data["newCaseFortnightAverage"], data["deathFortnightAverage"], data["newCasesToday"], data["startDate"], data["endDate"] );
        }

        public static Dictionary<string, string> ParseData(string dataString) {

            Dictionary<string, string> returnData = new Dictionary<string, string>();
            returnData.Add("startDate", "");
            returnData.Add("endDate", "");
            returnData.Add("newCasesToday", "");
            returnData.Add("newCaseFortnightAverage", "");
            returnData.Add("deathFortnightAverage", "");

            // Data string is csv, parse to JSON instead
            StringBuilder stringData = new StringBuilder();
            stringData.Append("{\"data\":");
            using (var p = ChoCSVReader.LoadText(dataString).WithFirstLineHeader()) {
                using (var w = new ChoJSONWriter(stringData)) {
                    w.Write(p);
                }
            }
            stringData.Append("}");

            JObject dataObject = JObject.Parse(stringData.ToString());
            JToken dataToken = dataObject["data"];
            JEnumerable<JToken> dataUnits = dataToken.Children();

            List<dataPoint> dataPoints = new List<dataPoint>();
            int newCaseCount;
            int deathCount;

            foreach (JToken token in dataUnits) {
                // Filter out any data that's not for Vic
                if (token.SelectToken("state").ToString() != "\\\"Victoria\\\"") continue;

                Int32.TryParse(token.SelectToken("confirmed").ToString(), out newCaseCount);
                Int32.TryParse(token.SelectToken("deaths").ToString(), out deathCount);

                dataPoints.Add(new dataPoint {
                    dateTime = DateTime.Parse(token.SelectToken("date").ToString()),
                    newCaseCount = newCaseCount,
                    deathCount = deathCount
                });
            }

            foreach (dataPoint data in dataPoints) {
                Console.WriteLine(data.dateTime + " " + data.newCaseCount + " " + data.deathCount);
            }

            dataPoint[] dataArray = dataPoints.ToArray();

            int index = dataArray.Length - 1;
            double newCaseFortnightAverage = 0;
            double deathFortnightAverage = 0;

            for(int i = 0; i < 13; i++) {
                newCaseCount = dataArray[index - i].newCaseCount;
                newCaseFortnightAverage += newCaseCount;
                deathCount = dataArray[index - i].deathCount;
                deathFortnightAverage += deathCount;
            }

            returnData["newCaseFortnightAverage"] = Math.Round(newCaseFortnightAverage / 14, 1).ToString();
            returnData["deathFortnightAverage"] = Math.Round(deathFortnightAverage / 14, 1).ToString();
            returnData["endDate"] = dataArray[index].dateTime.ToString("dddd, dd MMMM yyyy");
            returnData["startDate"] = dataArray[index - 13].dateTime.ToString("dddd, dd MMMM yyyy");
            returnData["newCasesToday"] = dataArray[index].newCaseCount.ToString();

            return returnData;
        }

        public static string GetData(string url)
        {
            var client = new WebClient();
            var response = client.DownloadString(url);
            return response;
        }

        public static void AnnounceToDiscord(string[] discordWebhookUrls, string newCaseFortnightAverage, string deathFortnightAverage, string newCase24Hours, string startDate, string endDate) 
        {
            Console.WriteLine("Announcing to " + discordWebhookUrls.Length + " Discord channels");

            var embed = ConstructEmbed(newCaseFortnightAverage, deathFortnightAverage, newCase24Hours, startDate, endDate);

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

        public static Embed ConstructEmbed(string newCaseFortnightAverage, string deathFortnightAverage, string newCase24Hours, string startDate, string endDate) 
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
            .WithName("New cases in the last 24 hours:")
            .WithValue("`" + newCase24Hours + "`");

            var aveNewCasesField = new EmbedFieldBuilder()
            .WithName("Average of new cases:")
            .WithValue("`" + newCaseFortnightAverage + "`")
            .WithIsInline(true);

            var aveDeathsField = new EmbedFieldBuilder()
            .WithName("Average of deaths:")
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
                aveNewCasesField,
                aveDeathsField,
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
