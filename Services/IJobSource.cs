using JobWidget.Models;

namespace JobWidget.Services
{
    /// <summary>
    /// Interface that all job sources must implement.
    /// Defines the contract for fetching jobs from any source.
    /// </summary>
    public interface IJobSource
    {
        /// <summary>
        /// Name of the job source (e.g. "Adzuna", "Indeed")
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Whether this source is currently enabled in config
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Fetch job postings from this source with the given parameters.
        /// </summary>
        Task<List<JobPosting>> FetchJobsAsync(
            string keywords,
            string? location = null,
            int resultsCount = 20,
            int? salaryMin = null,
            string? workMode = null);
    }
}