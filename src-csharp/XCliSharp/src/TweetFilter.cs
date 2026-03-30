// Tweet filtering and engagement scoring for XCliSharp.
// Mirrors twitter_cli/filter.py

namespace XCliSharp;

public static class TweetFilter
{
    public static readonly Dictionary<string, double> DefaultWeights = new()
    {
        ["likes"] = 1.0,
        ["retweets"] = 3.0,
        ["replies"] = 2.0,
        ["bookmarks"] = 5.0,
        ["views_log"] = 0.5,
    };

    /// <summary>Calculate engagement score for a single tweet.</summary>
    public static double ScoreTweet(Tweet tweet, Dictionary<string, double>? weights = null)
    {
        var w = weights ?? DefaultWeights;
        var m = tweet.Metrics;
        return w.GetValueOrDefault("likes", 1.0) * m.Likes
             + w.GetValueOrDefault("retweets", 3.0) * m.Retweets
             + w.GetValueOrDefault("replies", 2.0) * m.Replies
             + w.GetValueOrDefault("bookmarks", 5.0) * m.Bookmarks
             + w.GetValueOrDefault("views_log", 0.5) * Math.Log10(Math.Max(m.Views, 1));
    }

    /// <summary>Filter and rank tweets according to config.</summary>
    public static List<Tweet> FilterTweets(IEnumerable<Tweet> tweets, FilterConfig config)
    {
        var filtered = tweets.ToList();

        // 1. Language filter
        if (config.Lang is { Count: > 0 })
        {
            var langSet = new HashSet<string>(config.Lang, StringComparer.OrdinalIgnoreCase);
            filtered = filtered.Where(t => langSet.Contains(t.Lang)).ToList();
        }

        // 2. Exclude retweets
        if (config.ExcludeRetweets)
            filtered = filtered.Where(t => !t.IsRetweet).ToList();

        // 3. Score all tweets
        var weights = BuildWeights(config.Weights);
        var scored = filtered.Select(t =>
            t with { Score = Math.Round(ScoreTweet(t, weights), 1) }).ToList();

        // 4. Sort by score descending
        scored.Sort((a, b) => (b.Score ?? 0.0).CompareTo(a.Score ?? 0.0));

        // 5. Apply filter mode
        return config.Mode switch
        {
            FilterMode.TopN => scored.Take(Math.Max(config.TopN, 1)).ToList(),
            FilterMode.Score => scored.Where(t => (t.Score ?? 0.0) >= config.MinScore).ToList(),
            _ => scored, // All
        };
    }

    private static Dictionary<string, double> BuildWeights(Dictionary<string, double>? rawWeights)
    {
        var merged = new Dictionary<string, double>(DefaultWeights);
        if (rawWeights is null) return merged;
        foreach (var (key, value) in rawWeights)
            merged[key] = value;
        return merged;
    }
}

public enum FilterMode { TopN, Score, All }

public record FilterConfig(
    FilterMode Mode = FilterMode.TopN,
    int TopN = 20,
    double MinScore = 50.0,
    List<string>? Lang = null,
    bool ExcludeRetweets = false,
    Dictionary<string, double>? Weights = null
);
