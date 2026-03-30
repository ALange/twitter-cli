// Configuration loading for XCliSharp.
// Mirrors twitter_cli/config.py

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace XCliSharp;

public class AppConfig
{
    public FetchConfig Fetch { get; set; } = new();
    public FilterConfigYaml Filter { get; set; } = new();
    public RateLimitConfig RateLimit { get; set; } = new();
}

public class FetchConfig
{
    public int Count { get; set; } = 50;
}

public class FilterConfigYaml
{
    public string Mode { get; set; } = "topN";
    public int TopN { get; set; } = 20;
    public double MinScore { get; set; } = 50.0;
    public List<string> Lang { get; set; } = new();
    public bool ExcludeRetweets { get; set; } = false;
    public Dictionary<string, double> Weights { get; set; } = new()
    {
        ["likes"] = 1.0,
        ["retweets"] = 3.0,
        ["replies"] = 2.0,
        ["bookmarks"] = 5.0,
        ["views_log"] = 0.5,
    };

    public FilterConfig ToFilterConfig()
    {
        var mode = Mode?.ToLowerInvariant() switch
        {
            "topn" => FilterMode.TopN,
            "score" => FilterMode.Score,
            "all" => FilterMode.All,
            _ => FilterMode.TopN,
        };
        return new FilterConfig(
            Mode: mode,
            TopN: Math.Max(TopN, 1),
            MinScore: MinScore,
            Lang: Lang?.Count > 0 ? Lang : null,
            ExcludeRetweets: ExcludeRetweets,
            Weights: Weights
        );
    }
}

public class RateLimitConfig
{
    public double RequestDelay { get; set; } = 2.5;
    public int MaxRetries { get; set; } = 3;
    public double RetryBaseDelay { get; set; } = 5.0;
    public int MaxCount { get; set; } = 200;
}

public static class ConfigLoader
{
    public static AppConfig LoadConfig(string? configPath = null)
    {
        var path = ResolveConfigPath(configPath);
        if (path is null) return new AppConfig();

        try
        {
            var yaml = File.ReadAllText(path);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
            var parsed = deserializer.Deserialize<AppConfig>(yaml);
            return parsed ?? new AppConfig();
        }
        catch (Exception)
        {
            return new AppConfig();
        }
    }

    private static string? ResolveConfigPath(string? configPath)
    {
        if (configPath is not null)
            return File.Exists(configPath) ? configPath : null;

        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "config.yaml"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "config.yaml"),
        };

        foreach (var c in candidates)
            if (File.Exists(c)) return c;

        return null;
    }
}
