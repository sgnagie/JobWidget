using System.ComponentModel;

namespace JobWidget.Models
{
    public class JobPosting
    {
        public string Title { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string? SalaryDisplay { get; set; }

        public string DisplayText => $"{Title}  —  {Location}";
    }
}