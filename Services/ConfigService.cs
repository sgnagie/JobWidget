using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;

namespace JobWidget.Services
{
    /// <summary>
    /// Manages reading and writing the config.json file which stores API keys,
    /// search preferences, and enabled job sources.
    /// </summary>
    public class ConfigService
    {
        private readonly string _configPath;
        private AppConfig _config;

        public ConfigService(string configPath = "config.json")
        {
            _configPath = configPath;
            _config = Load();
        }

        public AppConfig Config => _config;

        /// <summary>
        /// Load config from disk, or create defaults if it doesn't exist.
        /// </summary>
        private AppConfig Load()
        {
            if (File.Exists(_configPath))
            {
                try
                {
                    var json = File.ReadAllText(_configPath);
                    return JsonSerializer.Deserialize<AppConfig>(json) ?? CreateDefaults();
                }
                catch
                {
                    // If parsing fails, fall back to defaults but keep the file
                    return CreateDefaults();
                }
            }

            // Create defaults and save them
            var config = CreateDefaults();
            Save(config);
            return config;
        }

        /// <summary>
        /// Save the current config to disk.
        /// </summary>
        public void Save()
        {
            Save(_config);
        }

        private void Save(AppConfig config)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(config, options);
            File.WriteAllText(_configPath, json);
        }

        private AppConfig CreateDefaults()
        {
            return new AppConfig
            {
                SearchKeywords = "software engineer",
                SearchLocation = "Louisville",
                SalaryMin = null,
                WorkMode = "Remote",
                RefreshIntervalMinutes = 10,
                Adzuna = new AdzunaConfig { Enabled = true, AppId = "", AppKey = "" },
                Remotive = new RemotiveConfig { Enabled = true },
                Jooble = new JoobleConfig { Enabled = false, ApiKey = "" },
                EnabledSources = new List<string> { "adzuna" },
                AutoRefreshEnabled = true,
            };
        }
    }

    public class AppConfig
    {
        [JsonPropertyName("searchKeywords")]
        public string SearchKeywords { get; set; } = "software engineer";

        [JsonPropertyName("searchLocation")]
        public string SearchLocation { get; set; } = "Louisville";

        [JsonPropertyName("salaryMin")]
        public int? SalaryMin { get; set; }

        [JsonPropertyName("workMode")]
        public string? WorkMode { get; set; }

        [JsonPropertyName("refreshIntervalMinutes")]
        public int RefreshIntervalMinutes { get; set; } = 10;

        [JsonPropertyName("autoRefreshEnabled")]
        public bool AutoRefreshEnabled { get; set; } = true;

        [JsonPropertyName("windowLeft")]
        public double? WindowLeft { get; set; }

        [JsonPropertyName("windowTop")]
        public double? WindowTop { get; set; }

        [JsonPropertyName("windowWidth")]
        public double? WindowWidth { get; set; }

        [JsonPropertyName("windowHeight")]
        public double? WindowHeight { get; set; }

        [JsonPropertyName("adzuna")]
        public AdzunaConfig Adzuna { get; set; } = new();

        [JsonPropertyName("remotive")]
        public RemotiveConfig Remotive { get; set; } = new();

        [JsonPropertyName("jooble")]
        public JoobleConfig Jooble { get; set; } = new();

        [JsonPropertyName("enabledSources")]
        public List<string> EnabledSources { get; set; } = new();
    }

    public class AdzunaConfig
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }

        [JsonPropertyName("appId")]
        public string AppId { get; set; } = "";

        [JsonPropertyName("appKey")]
        public string AppKey { get; set; } = "";
    }

    public class RemotiveConfig
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }
    }

    public class JoobleConfig
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }

        [JsonPropertyName("apiKey")]
        public string ApiKey { get; set; } = "";
    }
}
