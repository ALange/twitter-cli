// Twitter GraphQL API client for XCliSharp.
// Mirrors twitter_cli/client.py

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Web;

namespace XCliSharp;

public class TwitterClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _authToken;
    private readonly string _ct0;
    private readonly string? _cookieString;
    private readonly double _requestDelay;
    private readonly int _maxRetries;
    private readonly double _retryBaseDelay;
    private readonly int _maxCount;

    // Fallback queryIds (matches Python FALLBACK_QUERY_IDS)
    private static readonly Dictionary<string, string> FallbackQueryIds = new()
    {
        ["HomeTimeline"] = "L8Lb9oomccM012S7fQ-QKA",
        ["HomeLatestTimeline"] = "tzmrSIWxyV4IRRh9nij6TQ",
        ["UserByScreenName"] = "IGgvgiOx4QZndDHuD3x9TQ",
        ["UserTweets"] = "O0epvwaQPUx-bT9YlqlL6w",
        ["TweetDetail"] = "xIYgDwjboktoFeXe_fgacw",
        ["Likes"] = "RozQdCp4CilQzrcuU0NY5w",
        ["SearchTimeline"] = "rkp6b4vtR9u7v3naGoOzUQ",
        ["Bookmarks"] = "uzboyXSHSJrR-mGJqep0TQ",
        ["ListLatestTweetsTimeline"] = "fb_6wmHD2dk9D-xYXOQlgw",
        ["Followers"] = "Enf9DNUZYiT037aersI5gg",
        ["Following"] = "ntIPnH1WMBKW--4Tn1q71A",
        ["CreateTweet"] = "zkcFc6F-RKRgWN8HUkJfZg",
        ["DeleteTweet"] = "nxpZCY2K-I6QoFHAHeojFQ",
        ["FavoriteTweet"] = "lI07N6Otwv1PhnEgXILM7A",
        ["UnfavoriteTweet"] = "ZYKSe-w7KEslx3JhSIk5LA",
        ["CreateRetweet"] = "mbRO74GrOvSfRcJnlMapnQ",
        ["DeleteRetweet"] = "ZyZigVsNiFO6v1dEks1eWg",
        ["CreateBookmark"] = "aoDbu3RHznuiSkQ9aNM67Q",
        ["DeleteBookmark"] = "Wlmlj2-xzyS1GN3a6cj-Mq",
        ["TweetResultByRestId"] = "zy39CwTyYhU-_0LP7dljjg",
        ["BookmarkFoldersSlice"] = "i78YDd0Tza-dV4SYs58kRg",
        ["BookmarkFolderTimeline"] = "hNY7X2xE2N7HVF6Qb_mu6w",
        ["FollowUser"] = "oR-dB1Q4P4CVeZ-T5kNRiA",
        ["UnfollowUser"] = "9xr-B9BQBM6NsmLjPknUFQ",
    };

    // Default feature flags (compact — only True values sent)
    private static readonly Dictionary<string, bool> DefaultFeatures = new()
    {
        ["responsive_web_graphql_exclude_directive_enabled"] = true,
        ["creator_subscriptions_tweet_preview_api_enabled"] = true,
        ["responsive_web_graphql_timeline_navigation_enabled"] = true,
        ["c9s_tweet_anatomy_moderator_badge_enabled"] = true,
        ["tweetypie_unmention_optimization_enabled"] = true,
        ["responsive_web_edit_tweet_api_enabled"] = true,
        ["graphql_is_translatable_rweb_tweet_is_translatable_enabled"] = true,
        ["view_counts_everywhere_api_enabled"] = true,
        ["longform_notetweets_consumption_enabled"] = true,
        ["responsive_web_twitter_article_tweet_consumption_enabled"] = true,
        ["longform_notetweets_rich_text_read_enabled"] = true,
        ["longform_notetweets_inline_media_enabled"] = true,
        ["rweb_video_timestamps_enabled"] = true,
        ["responsive_web_media_download_video_enabled"] = true,
        ["freedom_of_speech_not_reach_fetch_enabled"] = true,
        ["standardized_nudges_misinfo"] = true,
    };

    public TwitterClient(
        string authToken,
        string ct0,
        RateLimitConfig? rateLimitConfig = null,
        string? cookieString = null)
    {
        _authToken = authToken;
        _ct0 = ct0;
        _cookieString = cookieString;

        var rl = rateLimitConfig ?? new RateLimitConfig();
        _requestDelay = rl.RequestDelay;
        _maxRetries = rl.MaxRetries;
        _retryBaseDelay = rl.RetryBaseDelay;
        _maxCount = Math.Min(rl.MaxCount, 500);

        var handler = new HttpClientHandler { UseCookies = false };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
    }

    // ── Read operations ──────────────────────────────────────────────────

    public async Task<List<Tweet>> FetchHomeTimelineAsync(int count = 20)
        => await FetchTimelineAsync("HomeTimeline", count,
            data => Parser.DeepGet(data, "data", "home", "home_timeline_urt", "instructions"));

    public async Task<List<Tweet>> FetchFollowingFeedAsync(int count = 20)
        => await FetchTimelineAsync("HomeLatestTimeline", count,
            data => Parser.DeepGet(data, "data", "home", "home_timeline_urt", "instructions"));

    public async Task<List<Tweet>> FetchBookmarksAsync(int count = 50)
        => await FetchTimelineAsync("Bookmarks", count, data =>
        {
            var instructions = Parser.DeepGet(data, "data", "bookmark_timeline", "timeline", "instructions");
            return instructions ?? Parser.DeepGet(data, "data", "bookmark_timeline_v2", "timeline", "instructions");
        });

    public async Task<UserProfile?> FetchUserAsync(string screenName)
    {
        var variables = new Dictionary<string, object>
        {
            ["screen_name"] = screenName,
            ["withSafetyModeUserFields"] = true,
        };
        var features = GetCompactFeatures();
        features["blue_business_profile_image_shape_enabled"] = true;

        var data = await GraphqlGetAsync("UserByScreenName", variables, features);
        var userResult = Parser.DeepGet(data, "data", "user", "result");
        return userResult.HasValue ? Parser.ParseUserResult(userResult.Value) : null;
    }

    public async Task<List<Tweet>> FetchUserTweetsAsync(string userId, int count = 20)
        => await FetchTimelineAsync("UserTweets", count,
            data => Parser.DeepGet(data, "data", "user", "result", "timeline_v2", "timeline", "instructions"),
            extraVariables: new Dictionary<string, object> { ["userId"] = userId });

    public async Task<List<Tweet>> FetchUserLikesAsync(string userId, int count = 20)
        => await FetchTimelineAsync("Likes", count,
            data => Parser.DeepGet(data, "data", "user", "result", "timeline_v2", "timeline", "instructions"),
            extraVariables: new Dictionary<string, object> { ["userId"] = userId });

    public async Task<List<Tweet>> FetchSearchAsync(string query, int count = 20, string product = "Top")
    {
        var variables = new Dictionary<string, object>
        {
            ["rawQuery"] = query,
            ["count"] = Math.Min(count, 20),
            ["querySource"] = "typed_query",
            ["product"] = product,
        };
        var features = GetCompactFeatures();
        var data = await GraphqlGetAsync("SearchTimeline", variables, features);
        var instructionsEl = Parser.DeepGet(data,
            "data", "search_by_raw_query", "search_timeline", "timeline", "instructions");
        if (!instructionsEl.HasValue) return new List<Tweet>();
        var (tweets, _) = Parser.ParseTimelineResponse(data, _ => instructionsEl);
        return tweets;
    }

    public async Task<(Tweet? Tweet, List<Tweet> Replies)> FetchTweetDetailAsync(string tweetId)
    {
        var variables = new Dictionary<string, object>
        {
            ["focalTweetId"] = tweetId,
            ["with_rux_injections"] = false,
            ["includePromotedContent"] = true,
            ["withCommunity"] = true,
            ["withQuickPromoteEligibilityTweetFields"] = true,
            ["withBirdwatchNotes"] = true,
            ["withVoice"] = true,
            ["withV2Timeline"] = true,
        };
        var data = await GraphqlGetAsync("TweetDetail", variables, GetCompactFeatures());
        var instructionsEl = Parser.DeepGet(data,
            "data", "threaded_conversation_with_injections_v2", "instructions");
        if (!instructionsEl.HasValue) return (null, new List<Tweet>());

        var (tweets, _) = Parser.ParseTimelineResponse(data, _ => instructionsEl);
        var focalTweet = tweets.FirstOrDefault(t => t.Id == tweetId);
        var replies = tweets.Where(t => t.Id != tweetId).ToList();
        return (focalTweet, replies);
    }

    public async Task<List<UserProfile>> FetchFollowersAsync(string userId, int count = 20)
        => await FetchUserListAsync("Followers", userId, count);

    public async Task<List<UserProfile>> FetchFollowingAsync(string userId, int count = 20)
        => await FetchUserListAsync("Following", userId, count);

    // ── Write operations ─────────────────────────────────────────────────

    public async Task<Tweet?> CreateTweetAsync(string text, string? replyToId = null)
    {
        var variables = new Dictionary<string, object>
        {
            ["tweet_text"] = text,
            ["dark_request"] = false,
            ["media"] = new Dictionary<string, object> { ["media_entities"] = new object[] { }, ["possibly_sensitive"] = false },
        };
        if (replyToId is not null)
            variables["reply"] = new Dictionary<string, object> { ["in_reply_to_tweet_id"] = replyToId, ["exclude_reply_user_ids"] = new object[] { } };

        var data = await GraphqlPostAsync("CreateTweet", variables, GetCompactFeatures());
        var result = Parser.DeepGet(data, "data", "create_tweet", "tweet_results", "result");
        return result.HasValue ? Parser.ParseTweetResult(result.Value) : null;
    }

    public async Task<Tweet?> DeleteTweetAsync(string tweetId)
    {
        var variables = new Dictionary<string, object>
        {
            ["tweet_id"] = tweetId,
            ["dark_request"] = false,
        };
        var data = await GraphqlPostAsync("DeleteTweet", variables, GetCompactFeatures());
        var result = Parser.DeepGet(data, "data", "delete_tweet", "tweet_results", "result");
        return result.HasValue ? Parser.ParseTweetResult(result.Value) : null;
    }

    public async Task<bool> LikeTweetAsync(string tweetId)
    {
        var data = await GraphqlPostAsync("FavoriteTweet",
            new Dictionary<string, object> { ["tweet_id"] = tweetId },
            GetCompactFeatures());
        var result = Parser.DeepGet(data, "data", "favorite_tweet");
        return result?.ValueKind == System.Text.Json.JsonValueKind.String && result.Value.GetString() == "Done";
    }

    public async Task<bool> UnlikeTweetAsync(string tweetId)
    {
        var data = await GraphqlPostAsync("UnfavoriteTweet",
            new Dictionary<string, object> { ["tweet_id"] = tweetId },
            GetCompactFeatures());
        var result = Parser.DeepGet(data, "data", "unfavorite_tweet");
        return result?.ValueKind == System.Text.Json.JsonValueKind.String;
    }

    public async Task<bool> RetweetAsync(string tweetId)
    {
        var data = await GraphqlPostAsync("CreateRetweet",
            new Dictionary<string, object> { ["tweet_id"] = tweetId, ["dark_request"] = false },
            GetCompactFeatures());
        return Parser.DeepGet(data, "data", "create_retweet", "retweet_results", "result") is not null;
    }

    public async Task<bool> UnretweetAsync(string tweetId)
    {
        var data = await GraphqlPostAsync("DeleteRetweet",
            new Dictionary<string, object> { ["tweet_id"] = tweetId, ["dark_request"] = false },
            GetCompactFeatures());
        return Parser.DeepGet(data, "data", "unretweet", "source_tweet_results", "result") is not null;
    }

    public async Task<bool> BookmarkTweetAsync(string tweetId)
    {
        var data = await GraphqlPostAsync("CreateBookmark",
            new Dictionary<string, object> { ["tweet_id"] = tweetId },
            GetCompactFeatures());
        return Parser.DeepGet(data, "data", "bookmark_tweet_result") is not null;
    }

    public async Task<bool> UnbookmarkTweetAsync(string tweetId)
    {
        var data = await GraphqlPostAsync("DeleteBookmark",
            new Dictionary<string, object> { ["tweet_id"] = tweetId },
            GetCompactFeatures());
        return Parser.DeepGet(data, "data", "tweet_bookmark_delete") is not null;
    }

    public async Task<bool> FollowUserAsync(string userId)
    {
        var formData = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("user_id", userId),
            new KeyValuePair<string, string>("skip_status", "true"),
        });
        var data = await ApiPostAsync("friendships/create.json", formData);
        return data.TryGetProperty("id_str", out _);
    }

    public async Task<bool> UnfollowUserAsync(string userId)
    {
        var formData = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("user_id", userId),
        });
        var data = await ApiPostAsync("friendships/destroy.json", formData);
        return data.TryGetProperty("id_str", out _);
    }

    // ── Internal helpers ─────────────────────────────────────────────────

    private async Task<List<Tweet>> FetchTimelineAsync(
        string operationName,
        int count,
        Func<System.Text.Json.JsonElement, System.Text.Json.JsonElement?> getInstructions,
        Dictionary<string, object>? extraVariables = null)
    {
        var allTweets = new List<Tweet>();
        string? cursor = null;
        int remaining = Math.Min(count, _maxCount);

        while (remaining > 0)
        {
            var variables = new Dictionary<string, object>
            {
                ["count"] = Math.Min(remaining, 20),
                ["includePromotedContent"] = true,
                ["latestControlAvailable"] = true,
                ["requestContext"] = "launch",
            };
            if (extraVariables is not null)
                foreach (var kv in extraVariables) variables[kv.Key] = kv.Value;
            if (cursor is not null) variables["cursor"] = cursor;

            var data = await GraphqlGetAsync(operationName, variables, GetCompactFeatures());
            var (tweets, nextCursor) = Parser.ParseTimelineResponse(data, getInstructions);

            allTweets.AddRange(tweets);
            remaining -= tweets.Count;

            if (nextCursor is null || tweets.Count == 0) break;
            cursor = nextCursor;

            await Task.Delay(TimeSpan.FromSeconds(_requestDelay));
        }

        return allTweets;
    }

    private async Task<List<UserProfile>> FetchUserListAsync(string operationName, string userId, int count)
    {
        var variables = new Dictionary<string, object>
        {
            ["userId"] = userId,
            ["count"] = Math.Min(count, 20),
        };
        var data = await GraphqlGetAsync(operationName, variables, GetCompactFeatures());
        var users = new List<UserProfile>();
        var instructionsEl = Parser.DeepGet(data, "data", "user", "result", "timeline", "timeline", "instructions");
        if (!instructionsEl.HasValue) return users;

        foreach (var instruction in instructionsEl.Value.EnumerateArray())
        {
            if (!instruction.TryGetProperty("entries", out var entries)) continue;
            foreach (var entry in entries.EnumerateArray())
            {
                var userResult = Parser.DeepGet(entry, "content", "itemContent", "user_results", "result");
                if (userResult.HasValue)
                {
                    var user = Parser.ParseUserResult(userResult.Value);
                    if (user is not null) users.Add(user);
                }
            }
        }
        return users;
    }

    private async Task<System.Text.Json.JsonElement> GraphqlGetAsync(
        string operationName,
        Dictionary<string, object> variables,
        Dictionary<string, bool> features)
    {
        var queryId = FallbackQueryIds.TryGetValue(operationName, out var qid) ? qid
            : throw new InvalidInputException($"Unknown operation: {operationName}");

        var variablesJson = JsonSerializer.Serialize(variables);
        var featuresJson = JsonSerializer.Serialize(features);
        var url = $"https://x.com/i/api/graphql/{queryId}/{operationName}" +
                  $"?variables={HttpUtility.UrlEncode(variablesJson)}" +
                  $"&features={HttpUtility.UrlEncode(featuresJson)}";

        return await SendWithRetryAsync(HttpMethod.Get, url, null);
    }

    private async Task<System.Text.Json.JsonElement> GraphqlPostAsync(
        string operationName,
        Dictionary<string, object> variables,
        Dictionary<string, bool> features)
    {
        var queryId = FallbackQueryIds.TryGetValue(operationName, out var qid) ? qid
            : throw new InvalidInputException($"Unknown operation: {operationName}");

        var url = $"https://x.com/i/api/graphql/{queryId}/{operationName}";
        var body = JsonSerializer.Serialize(new { variables, features, queryId });
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        return await SendWithRetryAsync(HttpMethod.Post, url, content);
    }

    private async Task<System.Text.Json.JsonElement> ApiPostAsync(string endpoint, HttpContent formContent)
    {
        var url = $"https://api.x.com/1.1/{endpoint}";
        return await SendWithRetryAsync(HttpMethod.Post, url, formContent);
    }

    private async Task<System.Text.Json.JsonElement> SendWithRetryAsync(
        HttpMethod method, string url, HttpContent? content)
    {
        int attempt = 0;
        while (true)
        {
            try
            {
                var request = new HttpRequestMessage(method, url);
                if (content is not null) request.Content = content;
                AddHeaders(request);

                var response = await _http.SendAsync(request);

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    throw new RateLimitException("Rate limited by Twitter API");
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    throw new AuthenticationException("Authentication failed — check your cookies");
                if (!response.IsSuccessStatusCode)
                    throw new TwitterApiException((int)response.StatusCode, response.ReasonPhrase ?? "Unknown error");

                var json = await response.Content.ReadAsStringAsync();
                return JsonDocument.Parse(json).RootElement;
            }
            catch (TwitterApiException) { throw; }
            catch (RateLimitException) { throw; }
            catch (AuthenticationException) { throw; }
            catch (Exception ex) when (attempt < _maxRetries)
            {
                attempt++;
                var delay = _retryBaseDelay * Math.Pow(2, attempt - 1);
                await Task.Delay(TimeSpan.FromSeconds(delay));
                _ = ex; // suppress warning
            }
            catch (Exception ex)
            {
                throw new NetworkException($"Network error: {ex.Message}", ex);
            }
        }
    }

    private void AddHeaders(HttpRequestMessage request)
    {
        var cookieHeader = _cookieString ?? $"auth_token={_authToken}; ct0={_ct0}";
        request.Headers.TryAddWithoutValidation("cookie", cookieHeader);
        request.Headers.TryAddWithoutValidation("authorization", $"Bearer {Constants.BearerToken}");
        request.Headers.TryAddWithoutValidation("x-csrf-token", _ct0);
        request.Headers.TryAddWithoutValidation("x-twitter-auth-type", "OAuth2Session");
        request.Headers.TryAddWithoutValidation("x-twitter-active-env", "production");
        request.Headers.TryAddWithoutValidation("x-twitter-client-language", Constants.GetTwitterClientLanguage());
        request.Headers.TryAddWithoutValidation("user-agent", Constants.GetUserAgent());
        request.Headers.TryAddWithoutValidation("accept", "*/*");
        request.Headers.TryAddWithoutValidation("accept-language", Constants.GetAcceptLanguage());
        request.Headers.TryAddWithoutValidation("sec-ch-ua", Constants.GetSecChUa());
        request.Headers.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
        request.Headers.TryAddWithoutValidation("sec-ch-ua-platform", Constants.GetSecChUaPlatform());
        request.Headers.TryAddWithoutValidation("sec-fetch-dest", "empty");
        request.Headers.TryAddWithoutValidation("sec-fetch-mode", "cors");
        request.Headers.TryAddWithoutValidation("sec-fetch-site", "same-origin");
        request.Headers.TryAddWithoutValidation("referer", "https://x.com/");
        request.Headers.TryAddWithoutValidation("origin", "https://x.com");
    }

    private static Dictionary<string, bool> GetCompactFeatures()
        => new(DefaultFeatures);

    public void Dispose() => _http.Dispose();
}
