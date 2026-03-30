// Tests for Config loading - mirrors test_config.py
using XCliSharp;
using Xunit;

namespace XCliSharp.Tests;

public class ConfigTests
{
    [Fact]
    public void LoadConfig_NoFile_ReturnsDefaults()
    {
        var config = ConfigLoader.LoadConfig("/nonexistent/path/config.yaml");

        Assert.Equal(50, config.Fetch.Count);
        Assert.Equal("topN", config.Filter.Mode, ignoreCase: true);
        Assert.Equal(20, config.Filter.TopN);
        Assert.Equal(50.0, config.Filter.MinScore);
        Assert.False(config.Filter.ExcludeRetweets);
        Assert.Equal(2.5, config.RateLimit.RequestDelay);
        Assert.Equal(3, config.RateLimit.MaxRetries);
    }

    [Fact]
    public void LoadConfig_ValidYaml_ParsesCorrectly()
    {
        var yaml = """
            fetch:
              count: 30
            filter:
              mode: score
              topN: 10
              minScore: 75.0
              excludeRetweets: true
              lang:
                - en
                - fr
            rateLimit:
              requestDelay: 1.5
              maxRetries: 5
            """;

        var tmpFile = Path.GetTempFileName();
        File.WriteAllText(tmpFile, yaml);

        try
        {
            var config = ConfigLoader.LoadConfig(tmpFile);
            Assert.Equal(30, config.Fetch.Count);
            Assert.Equal("score", config.Filter.Mode, ignoreCase: true);
            Assert.Equal(10, config.Filter.TopN);
            Assert.Equal(75.0, config.Filter.MinScore);
            Assert.True(config.Filter.ExcludeRetweets);
            Assert.Contains("en", config.Filter.Lang);
            Assert.Contains("fr", config.Filter.Lang);
            Assert.Equal(1.5, config.RateLimit.RequestDelay);
            Assert.Equal(5, config.RateLimit.MaxRetries);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    [Fact]
    public void FilterConfigYaml_ToFilterConfig_ConvertsTopN()
    {
        var fc = new FilterConfigYaml { Mode = "topN", TopN = 15 };
        var result = fc.ToFilterConfig();
        Assert.Equal(FilterMode.TopN, result.Mode);
        Assert.Equal(15, result.TopN);
    }

    [Fact]
    public void FilterConfigYaml_ToFilterConfig_ConvertsScore()
    {
        var fc = new FilterConfigYaml { Mode = "score", MinScore = 60.0 };
        var result = fc.ToFilterConfig();
        Assert.Equal(FilterMode.Score, result.Mode);
        Assert.Equal(60.0, result.MinScore);
    }

    [Fact]
    public void FilterConfigYaml_ToFilterConfig_ConvertsAll()
    {
        var fc = new FilterConfigYaml { Mode = "all" };
        var result = fc.ToFilterConfig();
        Assert.Equal(FilterMode.All, result.Mode);
    }

    [Fact]
    public void FilterConfigYaml_ToFilterConfig_InvalidMode_DefaultsToTopN()
    {
        var fc = new FilterConfigYaml { Mode = "invalid_mode" };
        var result = fc.ToFilterConfig();
        Assert.Equal(FilterMode.TopN, result.Mode);
    }

    [Fact]
    public void FilterConfigYaml_ToFilterConfig_WithLang_IncludesLang()
    {
        var fc = new FilterConfigYaml { Mode = "all", Lang = new List<string> { "en", "de" } };
        var result = fc.ToFilterConfig();
        Assert.NotNull(result.Lang);
        Assert.Contains("en", result.Lang);
        Assert.Contains("de", result.Lang);
    }

    [Fact]
    public void FilterConfigYaml_ToFilterConfig_EmptyLang_ReturnsNull()
    {
        var fc = new FilterConfigYaml { Mode = "all", Lang = new List<string>() };
        var result = fc.ToFilterConfig();
        Assert.Null(result.Lang);
    }
}
