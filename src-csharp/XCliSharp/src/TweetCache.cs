// Tweet cache for XCliSharp.
// Mirrors twitter_cli/cache.py

using System.Text.Json;

namespace XCliSharp;

public static class TweetCache
{
    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".twitter-cli");
    private static readonly string CacheFile = Path.Combine(CacheDir, "last_results.json");
    private const int TtlSeconds = 3600;

    public static void SaveTweetCache(IEnumerable<Tweet> tweets)
    {
        try
        {
            Directory.CreateDirectory(CacheDir);
            var entries = tweets
                .Select((t, i) => new { index = i + 1, id = t.Id, author = t.Author.ScreenName, text = t.Text.Length > 80 ? t.Text[..80] : t.Text })
                .Where(e => !string.IsNullOrEmpty(e.id))
                .ToList();
            var payload = new
            {
                created_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                tweets = entries,
            };
            File.WriteAllText(CacheFile, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception) { /* best-effort */ }
    }

    public static (string? TweetId, int CacheSize) ResolveCachedTweet(int index)
    {
        try
        {
            if (!File.Exists(CacheFile)) return (null, 0);
            var json = File.ReadAllText(CacheFile);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var createdAt = root.GetProperty("created_at").GetInt64();
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - createdAt > TtlSeconds)
                return (null, 0);

            var tweetsArr = root.GetProperty("tweets");
            var all = tweetsArr.EnumerateArray().ToList();
            foreach (var entry in all)
            {
                if (entry.TryGetProperty("index", out var idxProp) && idxProp.GetInt32() == index)
                {
                    var id = entry.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                    return (id, all.Count);
                }
            }
            return (null, all.Count);
        }
        catch (Exception) { return (null, 0); }
    }
}
