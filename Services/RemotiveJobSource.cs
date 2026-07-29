using System.Text.Json;
using System.Text.Json.Serialization;
using JobWidget.Models;
using System.Net.Http;

namespace JobWidget.Services
{
    /// <summary>
    /// Job source implementation for the Remotive API.
    /// Remotive only lists fully remote jobs and requires no API key.
    /// </summary>
    public class RemotiveJobSource : IJobSource
    {
        private readonly HttpClient _httpClient = new();
        private readonly ConfigService _configService;

        public string Name => "Remotive";
        public bool IsEnabled => _configService.Config.Remotive.Enabled;

        public RemotiveJobSource(ConfigService configService)
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
            // Remotive is remote-only. If the user has explicitly asked for
            // Hybrid or On-site, there's nothing this source can offer.
            if (!string.IsNullOrWhiteSpace(workMode)
                && !workMode.Equals("Remote", StringComparison.OrdinalIgnoreCase)
                && !workMode.Equals("Any", StringComparison.OrdinalIgnoreCase))
            {
                return new List<JobPosting>();
            }

            var url = "https://remotive.com/api/remote-jobs" +
                      $"?search={Uri.EscapeDataString(keywords)}" +
                      $"&limit={resultsCount}";

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

                    var parsed = JsonSerializer.Deserialize<RemotiveResponse>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (parsed?.Jobs is null)
                    {
                        return new List<JobPosting>();
                    }

                    var jobs = parsed.Jobs.Select(r => new JobPosting
                    {
                        Title = r.Title ?? "Untitled position",
                        Location = string.IsNullOrWhiteSpace(r.CandidateRequiredLocation)
                            ? "Remote"
                            : r.CandidateRequiredLocation,
                        Company = r.CompanyName ?? "Unknown company",
                        Url = r.Url ?? string.Empty,
                        SalaryDisplay = string.IsNullOrWhiteSpace(r.Salary) ? null : r.Salary
                    });

                    // Best-effort client-side salary filter; Remotive's salary
                    // field is free text, so only filter when we can parse a number.
                    if (salaryMin.HasValue)
                    {
                        jobs = jobs.Where(j => !TryParseLeadingNumber(j.SalaryDisplay, out var val) || val >= salaryMin.Value);
                    }

                    return jobs.ToList();
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
                            Title = "Couldn't load jobs from Remotive",
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
                    Location = "Remotive service temporarily unavailable. Will retry next refresh cycle.",
                    Company = string.Empty
                }
            };
        }

        private static bool IsTransientStatusCode(System.Net.HttpStatusCode statusCode)
        {
            return statusCode is System.Net.HttpStatusCode.ServiceUnavailable
                or System.Net.HttpStatusCode.BadGateway
                or System.Net.HttpStatusCode.GatewayTimeout
                or System.Net.HttpStatusCode.InternalServerError;
        }

        private static bool TryParseLeadingNumber(string? text, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var digits = new string(text.Where(c => char.IsDigit(c)).ToArray());
            return !string.IsNullOrEmpty(digits) && int.TryParse(digits, out value);
        }

        // ---- DTOs matching Remotive's JSON response ----
        private class RemotiveResponse
        {
            [JsonPropertyName("jobs")]
            public List<RemotiveJob>? Jobs { get; set; }
        }

        private class RemotiveJob
        {
            [JsonPropertyName("title")]
            public string? Title { get; set; }

            [JsonPropertyName("company_name")]
            public string? CompanyName { get; set; }

            [JsonPropertyName("url")]
            public string? Url { get; set; }

            [JsonPropertyName("candidate_required_location")]
            public string? CandidateRequiredLocation { get; set; }

            [JsonPropertyName("salary")]
            public string? Salary { get; set; }
        }
    }
}
