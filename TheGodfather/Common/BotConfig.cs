﻿#region USING_DIRECTIVES
using DSharpPlus;
using Newtonsoft.Json;
using System;
using System.IO;
#endregion

namespace TheGodfather.Common
{
    public sealed class BotConfig
    {
        [JsonProperty("db-config")]
        public DatabaseConfig DatabaseConfig { get; private set; }

        [JsonProperty("db_sync_interval")]
        public int DatabaseSyncInterval { get; private set; }

        [JsonProperty("prefix")]
        public string DefaultPrefix { get; private set; }

        [JsonProperty("feed_check_interval")]
        public int FeedCheckInterval { get; private set; }

        [JsonProperty("feed_check_start_delay")]
        public int FeedCheckStartDelay { get; private set; }

        [JsonProperty("key-giphy")]
        public string GiphyKey { get; private set; }

        [JsonProperty("key-goodreads")]
        public string GoodreadsKey { get; private set; }

        [JsonProperty("key-imgur")]
        public string ImgurKey { get; private set; }

        [JsonProperty("log-level")]
        public LogLevel LogLevel { get; private set; }

        [JsonProperty("log-path")]
        public string LogPath { get; private set; }

        [JsonProperty("log-to-file")]
        public bool LogToFile { get; private set; }

        [JsonProperty("key-omdb")]
        public string OMDbKey { get; private set; }

        [JsonProperty("shard-count")]
        public int ShardCount { get; private set; }

        [JsonProperty("key-steam")]
        public string SteamKey { get; private set; }

        [JsonProperty("token")]
        public string Token { get; private set; }

        [JsonProperty("key-weather")]
        public string WeatherKey { get; private set; }

        [JsonProperty("key-wit")]
        public string WitAiKey { get; private set; }

        [JsonProperty("key-youtube")]
        public string YouTubeKey { get; private set; }


        public static BotConfig Load()
        {
            if (!File.Exists("Resources/config.json"))
                return null;
            try {
                var cfg = JsonConvert.DeserializeObject<BotConfig>(File.ReadAllText("Resources/config.json"));
                return cfg;
            } catch (Exception e) {
                Console.WriteLine("EXCEPTION OCCURED WHILE LOADING CONFIG FILE: " + Environment.NewLine + e.ToString());
                return null;
            }
        }

        [JsonIgnore]
        public static BotConfig Default => new BotConfig {
            DatabaseConfig = DatabaseConfig.Default,
            DatabaseSyncInterval = 600,
            DefaultPrefix = "!",
            FeedCheckInterval = 300,
            FeedCheckStartDelay = 30,
            GiphyKey = "<insert GIPHY API key>",
            GoodreadsKey = "<insert Goodreads API key>",
            ImgurKey = "<insert Imgur API key>",
            LogLevel = LogLevel.Info,
            LogPath = "log.txt",
            LogToFile = false,
            OMDbKey = "<insert OMDb API key>",
            ShardCount = 1,
            SteamKey = "<insert Steam API key>",
            Token = "<insert bot token here>",
            WeatherKey = "<insert OpenWeatherMaps API key>",
            WitAiKey = "<insert Wit.ai key>",
            YouTubeKey = "<insert YouTube API key>"
        };
    }
}
