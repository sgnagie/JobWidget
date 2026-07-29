using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobWidget.Models;

namespace JobWidget.Services
{
    /// <summary>
    /// Job source implementation for the Adzuna API.
    /// </summary>
    public class AdzunaJobSource : IJobSource
    {
        private readonly HttpClient _httpClient = new();
        private readonly ConfigService _configService;

        private const string Country = "us";

        public string Name => "Adzuna";
        public bool IsEnabled => _configService.Config.Adzuna.Enabled;

        public AdzunaJobSource(ConfigService configService)
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
            var appId = _configService.Config.Adzuna.AppId;
            var appKey = _configService.Config.Adzuna.AppKey;

            // Validate credentials are set
            if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(appKey))
            {
                return new List<JobPosting>
                {
                    new JobPosting
                    {
                        Title = "Adzuna API credentials missing",
                        Location = "Add your App ID and App Key in settings (gear icon)",
                        Company = string.Empty
                    }
                };
            }

            // Fold work mode into the keyword search
            var searchKeywords = string.IsNullOrWhiteSpace(workMode)
                ? keywords
                : $"{keywords} {workMode}";

            var url = $"https://api.adzuna.com/v1/api/jobs/{Country}/search/1" +
                      $"?app_id={appId}&app_key={appKey}" +
                      $"&results_per_page={resultsCount}" +
                      $"&what={Uri.EscapeDataString(searchKeywords)}";

            if (!string.IsNullOrWhiteSpace(location))
            {
                url += $"&where={Uri.EscapeDataString(location)}";
            }

            if (salaryMin.HasValue)
            {
                url += $"&salary_min={salaryMin.Value}";
            }

            const int maxAttempts = 3;
            var retryDelay = TimeSpan.FromSeconds(5);

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    using var response = await _httpClient.GetAsync(url);

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
                    var json = System.Text.Encoding.UTF8.GetString(bytes);

                    var parsed = JsonSerializer.Deserialize<AdzunaResponse>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (parsed?.Results is null)
                    {
                        return new List<JobPosting>();
                    }

                    return parsed.Results.Select(r => new JobPosting
                    {
                        Title = r.Title ?? "Untitled position",
                        Location = r.Location?.DisplayName ?? "Location not specified",
                        Company = r.Company?.DisplayName ?? "Unknown company",
                        Url = r.RedirectUrl ?? string.Empty,
                        SalaryDisplay = FormatSalary(r.SalaryMin, r.SalaryMax)
                    }).ToList();
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
                            Title = "Couldn't load jobs from Adzuna",
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
                    Location = "Adzuna service temporarily unavailable. Will retry next refresh cycle.",
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

        private static string? FormatSalary(object? min, object? max)
        {
            var minVal = ParseSalary(min);
            var maxVal = ParseSalary(max);

            if (minVal is null && maxVal is null)
                return null;

            if (minVal.HasValue && maxVal.HasValue)
                return $"${minVal:N0} - ${maxVal:N0}";

            if (minVal.HasValue)
                return $"From ${minVal:N0}";

            return $"Up to ${maxVal:N0}";
        }

        private static int? ParseSalary(object? value)
        {
            if (value is null)
                return null;

            if (value is int intVal)
                return intVal;

            if (value is long longVal)
                return (int)longVal;

            if (int.TryParse(value.ToString(), out var result))
                return result;

            return null;
        }

        // ---- DTOs matching Adzuna's JSON response ----
        private class AdzunaResponse
        {
            [JsonPropertyName("results")]
            public List<AdzunaResult>? Results { get; set; }
        }

        private class AdzunaResult
        {
            [JsonPropertyName("title")]
            public string? Title { get; set; }

            [JsonPropertyName("location")]
            public AdzunaLocation? Location { get; set; }

            [JsonPropertyName("company")]
            public AdzunaCompany? Company { get; set; }

            [JsonPropertyName("redirect_url")]
            public string? RedirectUrl { get; set; }

            [JsonPropertyName("salary_min")]
            public object? SalaryMin { get; set; }

            [JsonPropertyName("salary_max")]
            public object? SalaryMax { get; set; }
        }

        private class AdzunaLocation
        {
            [JsonPropertyName("display_name")]
            public string? DisplayName { get; set; }
        }

        private class AdzunaCompany
        {
            [JsonPropertyName("display_name")]
            public string? DisplayName { get; set; }
        }
    }
}