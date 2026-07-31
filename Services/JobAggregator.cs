using JobWidget.Models;

namespace JobWidget.Services
{
    /// <summary>
    /// Aggregates job postings from multiple job sources.
    /// Fetches from all enabled sources in parallel and merges the results.
    /// </summary>
    public class JobAggregator
    {
        private readonly List<IJobSource> _sources = new();

        /// <summary>
        /// Sources that failed on the most recent fetch, with their error messages.
        /// </summary>
        public IReadOnlyList<(string Source, string Error)> LastErrors { get; private set; }
            = new List<(string, string)>();

        public JobAggregator(params IJobSource[] sources)
        {
            _sources.AddRange(sources);
        }

        /// <summary>
        /// Fetch jobs from all enabled sources and merge results.
        /// </summary>
        public async Task<List<JobPosting>> FetchJobsAsync(
            string keywords,
            string? location = null,
            int resultsPerSource = 20,
            int? salaryMin = null,
            string? workMode = null)
        {
            // Create fetch tasks for each enabled source
            LastErrors = new List<(string, string)>();
            var enabledSources = _sources.Where(s => s.IsEnabled).ToList();

            if (!enabledSources.Any())
            {
                return new List<JobPosting>
                {
                    new JobPosting
                    {
                        Title = "No job sources enabled",
                        Location = "Enable at least one source in settings (gear icon)",
                        Company = string.Empty
                    }
                };
            }

            var errors = new List<(string, string)>();

            var tasks = enabledSources.Select(source =>
                FetchSafelyAsync(source, errors, keywords, location,
                                 resultsPerSource, salaryMin, workMode)
            ).ToList();

            // Fetch from all sources in parallel; individual failures are isolated
            var results = await Task.WhenAll(tasks);

            LastErrors = errors;

            // Merge and deduplicate by URL
            var mergedJobs = new Dictionary<string, JobPosting>();

            foreach (var sourceJobs in results)
            {
                foreach (var job in sourceJobs)
                {
                    // Use URL as key for deduplication; if empty, use title+company+location combo
                    var key = !string.IsNullOrWhiteSpace(job.Url)
                        ? job.Url
                        : $"{job.Title}|{job.Company}|{job.Location}";

                    if (!mergedJobs.ContainsKey(key))
                    {
                        mergedJobs[key] = job;
                    }
                }
            }

            // Return merged list, sorted by title
            return mergedJobs.Values
                .OrderBy(j => j.Title)
                .ToList();
        }

        private async Task<List<JobPosting>> FetchSafelyAsync(
            IJobSource source,
            List<(string, string)> errors,
            string keywords,
            string? location,
            int resultsPerSource,
            int? salaryMin,
            string? workMode)
        {
            try
            {
                return await source.FetchJobsAsync(
                    keywords, location, resultsPerSource, salaryMin, workMode);
            }
            catch (Exception ex)
            {
                lock (errors)
                {
                    errors.Add((source.Name, ex.Message));
                }
                System.Diagnostics.Debug.WriteLine(
                    $"[JobAggregator] {source.Name} failed: {ex.Message}");
                return new List<JobPosting>();
            }
        }
    }
}