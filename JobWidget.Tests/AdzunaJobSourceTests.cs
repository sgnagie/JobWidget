using System.IO;
using System.Net;
using System.Net.Http;
using JobWidget.Services;
using JobWidget.Tests.Fakes;

namespace JobWidget.Tests
{
    public class AdzunaJobSourceTests : IDisposable
    {
        private readonly string _dir;
        private readonly ConfigService _config;

        public AdzunaJobSourceTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(_dir);
            _config = new ConfigService(Path.Combine(_dir, "config.json"));
            _config.Config.Adzuna.AppId = "test-id";
            _config.Config.Adzuna.AppKey = "test-key";
        }

        public void Dispose()
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, recursive: true);
        }

        private AdzunaJobSource SourceWith(StubHttpMessageHandler handler)
            => new AdzunaJobSource(
                _config,
                new HttpClient(handler),
                TimeSpan.FromMilliseconds(1));

        private const string OneJobJson = """
        {
          "results": [
            {
              "title": "Senior .NET Developer",
              "location": { "display_name": "Louisville, KY" },
              "company": { "display_name": "ACME Corp" },
              "redirect_url": "https://adzuna/job/1",
              "salary_min": 120000,
              "salary_max": 150000
            }
          ]
        }
        """;

        [Fact]
        public async Task ParsesAJobFromTheResponse()
        {
            var handler = new StubHttpMessageHandler().RespondWithJson(OneJobJson);

            var jobs = await SourceWith(handler).FetchJobsAsync("dotnet");

            var job = Assert.Single(jobs);
            Assert.Equal("Senior .NET Developer", job.Title);
            Assert.Equal("Louisville, KY", job.Location);
            Assert.Equal("ACME Corp", job.Company);
            Assert.Equal("https://adzuna/job/1", job.Url);
        }

        [Fact]
        public async Task FormatsAnIntegerSalaryRange()
        {
            var handler = new StubHttpMessageHandler().RespondWithJson(OneJobJson);

            var jobs = await SourceWith(handler).FetchJobsAsync("dotnet");

            Assert.Equal("$120,000 - $150,000", jobs[0].SalaryDisplay);
        }

        [Fact]
        public async Task FormatsADecimalSalaryRange()
        {
            const string decimalSalaryJson = """
            {
              "results": [
                {
                  "title": "Dev",
                  "redirect_url": "https://adzuna/job/2",
                  "salary_min": 120000.0,
                  "salary_max": 150000.0
                }
              ]
            }
            """;
            var handler = new StubHttpMessageHandler().RespondWithJson(decimalSalaryJson);

            var jobs = await SourceWith(handler).FetchJobsAsync("dotnet");

            Assert.Equal("$120,000 - $150,000", jobs[0].SalaryDisplay);
        }

        [Fact]
        public async Task MissingFieldsFallBackToPlaceholders()
        {
            const string sparseJson = """
            { "results": [ { "redirect_url": "https://adzuna/job/3" } ] }
            """;
            var handler = new StubHttpMessageHandler().RespondWithJson(sparseJson);

            var jobs = await SourceWith(handler).FetchJobsAsync("dotnet");

            Assert.Equal("Untitled position", jobs[0].Title);
            Assert.Equal("Location not specified", jobs[0].Location);
            Assert.Equal("Unknown company", jobs[0].Company);
            Assert.Null(jobs[0].SalaryDisplay);
        }

        [Fact]
        public async Task MissingCredentials_ReturnsGuidanceWithoutCallingTheApi()
        {
            _config.Config.Adzuna.AppId = "";
            var handler = new StubHttpMessageHandler();

            var jobs = await SourceWith(handler).FetchJobsAsync("dotnet");

            Assert.Equal("Adzuna API credentials missing", jobs[0].Title);
            Assert.Equal(0, handler.CallCount);
        }

        [Fact]
        public async Task RetriesOnTransientErrorThenSucceeds()
        {
            var handler = new StubHttpMessageHandler()
                .RespondWith(HttpStatusCode.ServiceUnavailable)
                .RespondWithJson(OneJobJson);

            var jobs = await SourceWith(handler).FetchJobsAsync("dotnet");

            Assert.Equal(2, handler.CallCount);
            Assert.Single(jobs);
        }

        [Fact]
        public async Task SearchUrlIncludesKeywordsAndLocation()
        {
            var handler = new StubHttpMessageHandler().RespondWithJson(OneJobJson);

            await SourceWith(handler).FetchJobsAsync("dotnet lead", "Louisville");

            Assert.Contains("what=dotnet%20lead", handler.LastRequestUri);
            Assert.Contains("where=Louisville", handler.LastRequestUri);
        }
    }
}