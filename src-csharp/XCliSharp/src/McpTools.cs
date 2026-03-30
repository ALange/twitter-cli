// MCP tool definitions for XCliSharp.
// Exposes Twitter/X operations as MCP tools for LLM consumption.
// Mirrors twitter_cli/mcp_server.py

using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace XCliSharp;

/// <summary>
/// MCP tools that expose Twitter/X search and read operations to LLM clients.
/// Registered via <see cref="McpServerToolTypeAttribute"/> for automatic discovery.
/// </summary>
[McpServerToolType]
public sealed class McpTools
{
    private static TwitterClient MakeClient()
    {
        var cfg = ConfigLoader.LoadConfig();
        var creds = Auth.Resolve();
        return new TwitterClient(creds.AuthToken, creds.Ct0, cfg.RateLimit, creds.CookieString);
    }

    private static string TweetsToJson(IEnumerable<Tweet> tweets) =>
        JsonSerializer.Serialize(
            tweets.Select(t => new
            {
                id = t.Id,
                author = t.Author.ScreenName,
                text = t.Text,
                likes = t.Metrics.Likes,
                retweets = t.Metrics.Retweets,
                replies = t.Metrics.Replies,
                views = t.Metrics.Views,
                time = TimeUtil.FormatLocalTime(t.CreatedAt),
                timeIso = TimeUtil.FormatIso8601(t.CreatedAt),
                isRetweet = t.IsRetweet,
                lang = t.Lang,
            }),
            new JsonSerializerOptions { WriteIndented = true });

    /// <summary>
    /// Search Twitter/X tweets and return a JSON list of matching results.
    /// </summary>
    [McpServerTool(Name = "search_tweet")]
    [Description(
        "Search Twitter/X tweets. " +
        "Returns a JSON array of tweets matching the query. " +
        "Use from_user/to_user/lang/since/until/min_likes for advanced filtering.")]
    public async Task<string> SearchTweet(
        [Description("Search keywords (e.g. 'python asyncio').")]
        string query = "",
        [Description("Search tab: Top | Latest | Photos | Videos. Defaults to Top.")]
        string product = "Top",
        [Description("Only tweets FROM this user (handle without @).")]
        string? from_user = null,
        [Description("Only tweets directed TO this user.")]
        string? to_user = null,
        [Description("ISO 639-1 language code, e.g. 'en', 'fr', 'ja'.")]
        string? lang = null,
        [Description("Start date (YYYY-MM-DD, inclusive).")]
        string? since = null,
        [Description("End date (YYYY-MM-DD, inclusive).")]
        string? until = null,
        [Description("Minimum number of likes.")]
        int? min_likes = null,
        [Description("Minimum number of retweets.")]
        int? min_retweets = null,
        [Description("Maximum number of tweets to return (1–200). Default 20.")]
        int max_results = 20)
    {
        try
        {
            var builtQuery = SearchBuilder.BuildSearchQuery(
                query,
                fromUser: from_user,
                toUser: to_user,
                lang: lang,
                since: since,
                until: until,
                minLikes: min_likes,
                minRetweets: min_retweets);

            if (string.IsNullOrWhiteSpace(builtQuery))
                return JsonSerializer.Serialize(new { error = "Provide a query or at least one filter (e.g. from_user, lang)." });

            var validProducts = new HashSet<string> { "Top", "Latest", "Photos", "Videos" };
            if (!validProducts.Contains(product))
                return JsonSerializer.Serialize(new { error = $"product must be one of: {string.Join(", ", validProducts.Order())}" });

            var count = Math.Clamp(max_results, 1, 200);

            using var client = MakeClient();
            var tweets = await client.FetchSearchAsync(builtQuery, count, product);
            // Take(count) guards against the API returning extra results during pagination.
            return TweetsToJson(tweets.Take(count));
        }
        catch (InvalidInputException ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
        catch (AuthenticationException ex)
        {
            return JsonSerializer.Serialize(new { error = $"Authentication failed: {ex.Message}" });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Fetch the authenticated user's home timeline.
    /// </summary>
    [McpServerTool(Name = "get_home_timeline")]
    [Description("Fetch the authenticated user's home timeline (For You feed). Returns JSON array of tweets.")]
    public async Task<string> GetHomeTimeline(
        [Description("Maximum number of tweets to return (1–200). Default 20.")]
        int max_results = 20)
    {
        try
        {
            var count = Math.Clamp(max_results, 1, 200);
            using var client = MakeClient();
            var tweets = await client.FetchHomeTimelineAsync(count);
            return TweetsToJson(tweets.Take(count));
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Fetch tweets posted by a specific user.
    /// </summary>
    [McpServerTool(Name = "get_user_timeline")]
    [Description("Fetch tweets posted by a specific Twitter/X user. Returns JSON array of tweets.")]
    public async Task<string> GetUserTimeline(
        [Description("The user's @handle (without the leading @).")]
        string screen_name,
        [Description("Maximum number of tweets to return (1–200). Default 20.")]
        int max_results = 20)
    {
        try
        {
            var handle = screen_name.TrimStart('@');
            var count = Math.Clamp(max_results, 1, 200);
            using var client = MakeClient();
            var user = await client.FetchUserAsync(handle);
            if (user is null)
                return JsonSerializer.Serialize(new { error = $"User @{handle} not found." });
            var tweets = await client.FetchUserTweetsAsync(user.Id, count);
            return TweetsToJson(tweets.Take(count));
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Fetch a user's profile information.
    /// </summary>
    [McpServerTool(Name = "get_user_profile")]
    [Description("Fetch profile information for a Twitter/X user. Returns a JSON object with profile fields.")]
    public async Task<string> GetUserProfile(
        [Description("The user's @handle (without the leading @).")]
        string screen_name)
    {
        try
        {
            var handle = screen_name.TrimStart('@');
            using var client = MakeClient();
            var user = await client.FetchUserAsync(handle);
            if (user is null)
                return JsonSerializer.Serialize(new { error = $"User @{handle} not found." });
            return JsonSerializer.Serialize(Serialization.UserProfileToDict(user), new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }
}
