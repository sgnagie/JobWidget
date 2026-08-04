using System.Text.Json;
using JobWidget.Services;
using System.IO;

namespace JobWidget.Tests
{
    public class ConfigServiceTests : IDisposable
    {
        private readonly string _dir;
        private readonly string _path;

        public ConfigServiceTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(_dir);
            _path = Path.Combine(_dir, "config.json");
        }

        public void Dispose()
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, recursive: true);
        }

        [Fact]
        public void MissingFile_ProducesDefaults()
        {
            var svc = new ConfigService(_path);

            Assert.Equal("software engineer", svc.Config.SearchKeywords);
            Assert.Equal("Louisville", svc.Config.SearchLocation);
            Assert.Equal(10, svc.Config.RefreshIntervalMinutes);
            Assert.True(svc.Config.Adzuna.Enabled);
            Assert.False(svc.Config.Jooble.Enabled);
        }

        [Fact]
        public void MissingFile_WritesDefaultsToDisk()
        {
            Assert.False(File.Exists(_path));

            _ = new ConfigService(_path);

            Assert.True(File.Exists(_path));
        }

        [Fact]
        public void SavedValues_SurviveARoundTrip()
        {
            var first = new ConfigService(_path);
            first.Config.SearchKeywords = "dotnet lead";
            first.Config.SalaryMin = 140000;
            first.Config.WindowLeft = 42.5;
            first.Config.Jooble.ApiKey = "abc-123";
            first.Save();

            var second = new ConfigService(_path);

            Assert.Equal("dotnet lead", second.Config.SearchKeywords);
            Assert.Equal(140000, second.Config.SalaryMin);
            Assert.Equal(42.5, second.Config.WindowLeft);
            Assert.Equal("abc-123", second.Config.Jooble.ApiKey);
        }

        [Fact]
        public void MalformedJson_FallsBackToDefaultsWithoutThrowing()
        {
            File.WriteAllText(_path, "{ this is not json ");

            var svc = new ConfigService(_path);

            Assert.Equal("software engineer", svc.Config.SearchKeywords);
        }

        [Fact]
        public void MalformedJson_LeavesTheOriginalFileUntouched()
        {
            const string garbage = "{ this is not json ";
            File.WriteAllText(_path, garbage);

            _ = new ConfigService(_path);

            Assert.Equal(garbage, File.ReadAllText(_path));
        }

        [Fact]
        public void PartialJson_UsesPropertyInitializersNotCreateDefaults()
        {
            File.WriteAllText(_path, """{ "searchKeywords": "kept" }""");

            var svc = new ConfigService(_path);

            Assert.Equal("kept", svc.Config.SearchKeywords);
            // Adzuna section absent from the file, so the property initializer
            // `new()` runs — which leaves Enabled false, unlike CreateDefaults().
            Assert.False(svc.Config.Adzuna.Enabled);
        }
    }
}