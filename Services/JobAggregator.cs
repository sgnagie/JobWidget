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

            var tasks = enabledSources.Select(source =>
                source.FetchJobsAsync(keywords, location, resultsPerSource, salaryMin, workMode)
            ).ToList();

            // Fetch from all sources in parallel
            var results = await Task.WhenAll(tasks);

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
    }
}