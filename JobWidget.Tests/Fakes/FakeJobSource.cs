using JobWidget.Models;
using JobWidget.Services;

namespace JobWidget.Tests.Fakes
{
    public class FakeJobSource : IJobSource
    {
        private readonly List<JobPosting> _jobs;
        private readonly Exception? _throwOnFetch;

        public string Name { get; }
        public bool IsEnabled { get; }

        // Recording fields — let tests assert on how the aggregator called us.
        public int CallCount { get; private set; }
        public string? LastKeywords { get; private set; }

        public FakeJobSource(
            string name,
            List<JobPosting>? jobs = null,
            bool isEnabled = true,
            Exception? throwOnFetch = null)
        {
            Name = name;
            _jobs = jobs ?? new List<JobPosting>();
            IsEnabled = isEnabled;
            _throwOnFetch = throwOnFetch;
        }

        public async Task<List<JobPosting>> FetchJobsAsync(
            string keywords,
            string? location = null,
            int resultsCount = 20,
            int? salaryMin = null,
            string? workMode = null)
        {
            CallCount++;
            LastKeywords = keywords;

            await Task.Yield();

            if (_throwOnFetch != null)
                throw _throwOnFetch;

            return new List<JobPosting>(_jobs);
        }
    }
}