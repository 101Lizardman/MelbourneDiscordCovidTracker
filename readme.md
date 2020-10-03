# Covid :dizzy_face: lockdown :lock: tracker :hourglass:

Are you **sick** and **tired** of googling the 14 day average of new covid cases every day? 
Are you just counting down the days until you're **free**? :unlock:
Then this console app is for **you**!

It pulls data from a [Guardian Austraia](https://services1.arcgis.com/vHnIGBHHqDR6y0CR/arcgis/rest/services/COVID19_Time_Series/FeatureServer/0/query?where=1%3D1&outFields=*&outSR=4326&f=json) API, calculates the average and then shoots the message to a Discord channel, given by YOU! The user!

## Usage
In order to run the bot, ya gotta have an environment file. By default, you can make one in the root folder and call it `.env`.
In there, ya gotta have an environment variable called `DISCORD_WEBHOOK_URLS` and it's value is a comma delimited list of strings of Discord webhook urls.
```
    DISCORD_WEBHOOK_URLS=https://discordapp.com/api/webhooks/XXX/YYY
```

## Execution
`dotnet run`

Don't be an asshole :imp:, wear a mask! :mask: