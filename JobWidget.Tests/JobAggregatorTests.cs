using JobWidget.Models;
using JobWidget.Services;
using JobWidget.Tests.Fakes;
using System.Net.Http;

namespace JobWidget.Tests
{
    public class JobAggregatorTests
    {
        [Fact]
        public async Task MergesJobsFromAllEnabledSources()
        {
            var a = new FakeJobSource("A",
                new List<JobPosting> { Make.Job("Alpha Dev", "https://a/1") });
            var b = new FakeJobSource("B",
                new List<JobPosting> { Make.Job("Beta Dev", "https://b/1") });
 
            var results = await new JobAggregator(a, b).FetchJobsAsync("c#");

            Assert.Equal(2, results.Count);
        }

        [Fact]
        public async Task DeduplicatesJobsWithTheSameUrl()
        {
            var a = new FakeJobSource("A",
                new List<JobPosting> { Make.Job("Dev", "https://shared/1", company: "First") });
            var b = new FakeJobSource("B",
                new List<JobPosting> { Make.Job("Dev", "https://shared/1", company: "Second") });

            var results = await new JobAggregator(a, b).FetchJobsAsync("c#");

            Assert.Single(results);
            Assert.Equal("First", results[0].Company);
        }

        [Fact]
        public async Task SortsResultsByTitle()
        {
            var a = new FakeJobSource("A", new List<JobPosting>
            {
                Make.Job("Zebra Engineer", "https://a/1"),
                Make.Job("Alpha Engineer", "https://a/2"),
                Make.Job("Middle Engineer", "https://a/3"),
            });

            var results = await new JobAggregator(a).FetchJobsAsync("c#");

            Assert.Equal(
                new[] { "Alpha Engineer", "Middle Engineer", "Zebra Engineer" },
                results.Select(j => j.Title).ToArray());
        }

        [Fact]
        public async Task DisabledSourcesAreNeverCalled()
        {
            var on = new FakeJobSource("On",
                new List<JobPosting> { Make.Job("Dev", "https://on/1") });
            var off = new FakeJobSource("Off",
                new List<JobPosting> { Make.Job("Nope", "https://off/1") },
                isEnabled: false);

            var results = await new JobAggregator(on, off).FetchJobsAsync("c#");

            Assert.Equal(1, on.CallCount);
            Assert.Equal(0, off.CallCount);
            Assert.Single(results);
        }

        [Fact]
        public async Task NoEnabledSources_ReturnsPlaceholderMessage()
        {
            var off = new FakeJobSource("Off", isEnabled: false);

            var results = await new JobAggregator(off).FetchJobsAsync("c#");

            Assert.Single(results);
            Assert.Equal("No job sources enabled", results[0].Title);
        }

        [Fact]
        public async Task OneFailingSource_StillReturnsResultsFromOthers()
        {
            var good = new FakeJobSource("Good",
                new List<JobPosting> { Make.Job("Dev", "https://good/1") });
            var bad = new FakeJobSource("Bad",
                throwOnFetch: new HttpRequestException("source is down"));

            var results = await new JobAggregator(good, bad).FetchJobsAsync("c#");

            Assert.Single(results);
            Assert.Equal("https://good/1", results[0].Url);
        }

        [Fact]
        public async Task FailingSource_IsRecordedInLastErrors()
        {
            var good = new FakeJobSource("Good",
                new List<JobPosting> { Make.Job("Dev", "https://good/1") });
            var bad = new FakeJobSource("Bad",
                throwOnFetch: new HttpRequestException("source is down"));

            var aggregator = new JobAggregator(good, bad);
            await aggregator.FetchJobsAsync("c#");

            var error = Assert.Single(aggregator.LastErrors);
            Assert.Equal("Bad", error.Source);
            Assert.Contains("source is down", error.Error);
        }

        [Fact]
        public async Task LastErrors_IsClearedWhenNoSourcesAreEnabled()
        {
            var bad = new FakeJobSource("Bad",
                throwOnFetch: new HttpRequestException("source is down"));
            var aggregator = new JobAggregator(bad);

            await aggregator.FetchJobsAsync("c#");
            Assert.Single(aggregator.LastErrors);   // failure recorded

            var offOnly = new JobAggregator(
                new FakeJobSource("Off", isEnabled: false));
            await offOnly.FetchJobsAsync("c#");
            Assert.Empty(offOnly.LastErrors);
        }
    }
}