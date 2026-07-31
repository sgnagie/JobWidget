using JobWidget.Models;

namespace JobWidget.Tests.Fakes
{
    public static class Make
    {
        public static JobPosting Job(
            string title,
            string url,
            string company = "ACME",
            string location = "Remote")
            => new JobPosting
            {
                Title = title,
                Url = url,
                Company = company,
                Location = location
            };
    }
}