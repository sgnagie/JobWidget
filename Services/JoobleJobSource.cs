using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobWidget.Models;
using System.Net.Http;

namespace JobWidget.Services
{
    /// <summary>
    /// Job source implementation for the Jooble API.
    /// Jooble's API is POST-only, with the API key embedded in the URL path.
    /// </summary>
    public class JoobleJobSource : IJobSource
    {
        private readonly HttpClient _httpClient = new();
        private readonly ConfigService _configService;

        public string Name => "Jooble";
        public bool IsEnabled => _configService.Config.Jooble.Enabled;

        public JoobleJobSource(ConfigService configService)
        {
            _configService = configService;
        }

        public async Task<List<JobPosting>> FetchJobsAsync(
            string keywords,
            string? location = null,
            int resultsCount = 20,
            int? salaryMin = null,
            string? workMode = null)
        {
            var apiKey = _configService.Config.Jooble.ApiKey;

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return new List<JobPosting>
                {
                    new JobPosting
                    {
                        Title = "Jooble API key missing",
                        Location = "Add your API key in settings (gear icon)",
                        Company = string.Empty
                    }
                };
            }

            var searchKeywords = string.IsNullOrWhiteSpace(workMode)
                ? keywords
                : $"{keywords} {workMode}";

            var requestBody = new JoobleRequest
            {
                Keywords = searchKeywords,
                Location = location ?? string.Empty,
                Page = "1"
            };

            var url = $"https://jooble.org/api/{apiKey}";
            var jsonBody = JsonSerializer.Serialize(requestBody);

            const int maxAttempts = 3;
            var retryDelay = TimeSpan.FromSeconds(5);

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    using var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                    using var response = await _httpClient.PostAsync(url, content);

                    if (!response.IsSuccessStatusCode && IsTransientStatusCode(response.StatusCode))
                    {
                        if (attempt < maxAttempts)
                        {
                            await Task.Delay(retryDelay);
                            continue;
                        }
                    }

                    response.EnsureSuccessStatusCode();

                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    var json = Encoding.UTF8.GetString(bytes);

                    var parsed = JsonSerializer.Deserialize<JoobleResponse>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (parsed?.Jobs is null)
                    {
                        return new List<JobPosting>();
                    }

                    var results = parsed.Jobs.Take(resultsCount).Select(r => new JobPosting
                    {
                        Title = r.Title ?? "Untitled position",
                        Location = string.IsNullOrWhiteSpace(r.Location) ? "Location not specified" : r.Location,
                        Company = string.IsNullOrWhiteSpace(r.Company) ? "Unknown company" : r.Company,
                        Url = r.Link ?? string.Empty,
                        SalaryDisplay = string.IsNullOrWhiteSpace(r.Salary) ? null : r.Salary
                    });

                    // Best-effort client-side salary filter; Jooble's salary field
                    // is free text and multi-currency, so only filter when parseable.
                    if (salaryMin.HasValue)
                    {
                        results = results.Where(j => !TryParseLeadingNumber(j.SalaryDisplay, out var val) || val >= salaryMin.Value);
                    }

                    return results.ToList();
                }
                catch (HttpRequestException) when (attempt < maxAttempts)
                {
                    await Task.Delay(retryDelay);
                }
                catch (Exception ex)
                {
                    return new List<JobPosting>
                    {
                        new JobPosting
                        {
                            Title = "Couldn't load jobs from Jooble",
                            Location = ex.Message,
                            Company = string.Empty
                        }
                    };
                }
            }

            return new List<JobPosting>
            {
                new JobPosting
                {
                    Title = "Couldn't load jobs",
                    Location = "Jooble service temporarily unavailable. Will retry next refresh cycle.",
                    Company = string.Empty
                }
            };
        }

        private static bool IsTransientStatusCode(HttpStatusCode statusCode)
        {
            return statusCode is HttpStatusCode.ServiceUnavailable
                or HttpStatusCode.BadGateway
                or HttpStatusCode.GatewayTimeout
                or HttpStatusCode.InternalServerError;
        }

        private static bool TryParseLeadingNumber(string? text, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var digits = new string(text.Where(c => char.IsDigit(c)).ToArray());
            return !string.IsNullOrEmpty(digits) && int.TryParse(digits, out value);
        }

        // ---- DTOs matching Jooble's JSON request/response ----
        private class JoobleRequest
        {
            [JsonPropertyName("keywords")]
            public string Keywords { get; set; } = "";

            [JsonPropertyName("location")]
            public string Location { get; set; } = "";

            [JsonPropertyName("page")]
            public string Page { get; set; } = "1";
        }

        private class JoobleResponse
        {
            [JsonPropertyName("totalCount")]
            public int TotalCount { get; set; }

            [JsonPropertyName("jobs")]
            public List<JoobleJob>? Jobs { get; set; }
        }

        private class JoobleJob
        {
            [JsonPropertyName("title")]
            public string? Title { get; set; }

            [JsonPropertyName("location")]
            public string? Location { get; set; }

            [JsonPropertyName("company")]
            public string? Company { get; set; }

            [JsonPropertyName("link")]
            public string? Link { get; set; }

            [JsonPropertyName("salary")]
            public string? Salary { get; set; }
        }
    }
}
