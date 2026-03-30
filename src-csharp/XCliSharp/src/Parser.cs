// GraphQL response parser for XCliSharp.
// Mirrors twitter_cli/parser.py

using System.Text.Json;

namespace XCliSharp;

public static class Parser
{
    // ── Utility helpers ──────────────────────────────────────────────────

    /// <summary>Safely get a nested value from a JsonElement using a path of string keys.</summary>
    public static JsonElement? DeepGet(JsonElement? element, params string[] keys)
    {
        if (element is null) return null;
        var current = element.Value;
        foreach (var key in keys)
        {
            if (current.ValueKind != JsonValueKind.Object) return null;
            if (!current.TryGetProperty(key, out var next)) return null;
            current = next;
        }
        return current;
    }

    public static int ParseInt(JsonElement? element, int defaultValue = 0)
    {
        if (element is null || element.Value.ValueKind == JsonValueKind.Null) return defaultValue;
        try
        {
            return element.Value.ValueKind switch
            {
                JsonValueKind.Number => element.Value.TryGetInt32(out var i) ? i : (int)element.Value.GetDouble(),
                JsonValueKind.String => int.TryParse(
                    element.Value.GetString()?.Replace(",", "").Trim(), out var si) ? si : defaultValue,
                _ => defaultValue,
            };
        }
        catch { return defaultValue; }
    }

    private static string? ExtractCursor(JsonElement content)
    {
        if (content.TryGetProperty("cursorType", out var ct) && ct.GetString() == "Bottom")
            if (content.TryGetProperty("value", out var v))
                return v.GetString();
        return null;
    }

    // ── Media extraction ─────────────────────────────────────────────────

    private static List<TweetMedia> ExtractMedia(JsonElement legacy)
    {
        var result = new List<TweetMedia>();
        var mediaArr = DeepGet(legacy, "extended_entities", "media");
        if (mediaArr is null || mediaArr.Value.ValueKind != JsonValueKind.Array) return result;

        foreach (var item in mediaArr.Value.EnumerateArray())
        {
            var type = item.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
            int? width = DeepGet(item, "original_info", "width") is { } w ? ParseInt(w) : null;
            int? height = DeepGet(item, "original_info", "height") is { } h ? ParseInt(h) : null;

            if (type == "photo")
            {
                var url = item.TryGetProperty("media_url_https", out var u) ? u.GetString() ?? "" : "";
                result.Add(new TweetMedia("photo", url, width, height));
            }
            else if (type == "video" || type == "animated_gif")
            {
                var videoInfo = DeepGet(item, "video_info", "variants");
                string bestUrl = item.TryGetProperty("media_url_https", out var u2) ? u2.GetString() ?? "" : "";
                if (videoInfo?.ValueKind == JsonValueKind.Array)
                {
                    var mp4Variants = videoInfo.Value.EnumerateArray()
                        .Where(v => v.TryGetProperty("content_type", out var ct2) && ct2.GetString() == "video/mp4")
                        .Select(v => (url: v.TryGetProperty("url", out var vu) ? vu.GetString() ?? "" : "",
                                     bitrate: v.TryGetProperty("bitrate", out var vb) ? vb.GetInt32() : 0))
                        .OrderByDescending(v => v.bitrate)
                        .ToList();
                    if (mp4Variants.Count > 0) bestUrl = mp4Variants[0].url;
                }
                result.Add(new TweetMedia(type, bestUrl, width, height));
            }
        }
        return result;
    }

    private static Author ExtractAuthor(JsonElement userData, JsonElement userLegacy)
    {
        var userCore = userData.TryGetProperty("core", out var c) ? c : default;
        return new Author(
            Id: userData.TryGetProperty("rest_id", out var rid) ? rid.GetString() ?? "" : "",
            Name: GetStringFallback(
                userCore.ValueKind == JsonValueKind.Object ? userCore.TryGetProperty("name", out var n) ? n : default : default,
                userLegacy.TryGetProperty("name", out var ln) ? ln : default,
                userData.TryGetProperty("name", out var dn) ? dn : default) ?? "Unknown",
            ScreenName: GetStringFallback(
                userCore.ValueKind == JsonValueKind.Object ? userCore.TryGetProperty("screen_name", out var sn) ? sn : default : default,
                userLegacy.TryGetProperty("screen_name", out var lsn) ? lsn : default,
                userData.TryGetProperty("screen_name", out var dsn) ? dsn : default) ?? "unknown",
            ProfileImageUrl: userData.TryGetProperty("avatar", out var av) && av.TryGetProperty("image_url", out var iu)
                ? iu.GetString() ?? ""
                : (userLegacy.TryGetProperty("profile_image_url_https", out var piu) ? piu.GetString() ?? "" : ""),
            Verified: (userData.TryGetProperty("is_blue_verified", out var ibv) && ibv.GetBoolean())
                   || (userLegacy.TryGetProperty("verified", out var v) && v.GetBoolean())
        );
    }

    private static string? GetStringFallback(params JsonElement[] elements)
    {
        foreach (var el in elements)
            if (el.ValueKind == JsonValueKind.String)
            {
                var s = el.GetString();
                if (!string.IsNullOrEmpty(s)) return s;
            }
        return null;
    }

    // ── User parsing ─────────────────────────────────────────────────────

    public static UserProfile? ParseUserResult(JsonElement userData)
    {
        if (userData.TryGetProperty("__typename", out var tn) && tn.GetString() == "UserUnavailable")
            return null;

        if (!userData.TryGetProperty("legacy", out var legacy) || legacy.ValueKind != JsonValueKind.Object)
            return null;

        string? expandedUrl = null;
        var urlsEl = DeepGet(legacy, "entities", "url", "urls");
        if (urlsEl?.ValueKind == JsonValueKind.Array)
        {
            var urlsArr = urlsEl.Value.EnumerateArray().ToList();
            if (urlsArr.Count > 0)
                expandedUrl = urlsArr[0].TryGetProperty("expanded_url", out var eu) ? eu.GetString() : null;
        }

        return new UserProfile(
            Id: userData.TryGetProperty("rest_id", out var restId) ? restId.GetString() ?? "" : "",
            Name: legacy.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
            ScreenName: legacy.TryGetProperty("screen_name", out var sn) ? sn.GetString() ?? "" : "",
            Bio: legacy.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
            Location: legacy.TryGetProperty("location", out var loc) ? loc.GetString() ?? "" : "",
            Url: expandedUrl ?? "",
            FollowersCount: ParseInt(legacy.TryGetProperty("followers_count", out var fc) ? fc : default),
            FollowingCount: ParseInt(legacy.TryGetProperty("friends_count", out var frc) ? frc : default),
            TweetsCount: ParseInt(legacy.TryGetProperty("statuses_count", out var sc) ? sc : default),
            LikesCount: ParseInt(legacy.TryGetProperty("favourites_count", out var lc) ? lc : default),
            Verified: (userData.TryGetProperty("is_blue_verified", out var ibv) && ibv.GetBoolean())
                   || (legacy.TryGetProperty("verified", out var v) && v.GetBoolean()),
            ProfileImageUrl: legacy.TryGetProperty("profile_image_url_https", out var piu) ? piu.GetString() ?? "" : "",
            CreatedAt: legacy.TryGetProperty("created_at", out var cat) ? cat.GetString() ?? "" : ""
        );
    }

    // ── Tweet parsing ────────────────────────────────────────────────────

    private static (JsonElement data, bool isSubscriberOnly) UnwrapVisibility(JsonElement result)
    {
        if (result.TryGetProperty("__typename", out var tn) && tn.GetString() == "TweetWithVisibilityResults"
            && result.TryGetProperty("tweet", out var inner))
        {
            bool interstitial = result.TryGetProperty("tweetInterstitial", out var ti) && ti.ValueKind != JsonValueKind.Null;
            return (inner, interstitial);
        }
        return (result, false);
    }

    public static Tweet? ParseTweetResult(JsonElement result, int depth = 0)
    {
        if (depth > 2) return null;

        var (tweetData, isSubscriberOnly) = UnwrapVisibility(result);

        if (tweetData.TryGetProperty("__typename", out var typename) && typename.GetString() == "TweetTombstone")
            return null;

        if (!tweetData.TryGetProperty("legacy", out var legacy) || legacy.ValueKind != JsonValueKind.Object)
            return null;
        if (!tweetData.TryGetProperty("core", out var core) || core.ValueKind != JsonValueKind.Object)
            return null;

        var userResult = DeepGet(core, "user_results", "result");
        var userData = userResult ?? default(JsonElement?);
        var userLegacyEl = userData?.TryGetProperty("legacy", out var ul) == true ? ul : default;
        var userCoreEl = userData?.TryGetProperty("core", out var uc) == true ? uc : default;

        var isRetweet = DeepGet(legacy, "retweeted_status_result", "result") is not null;

        var actualData = tweetData;
        var actualLegacy = legacy;
        var actualUser = userData ?? default(JsonElement);
        var actualUserLegacy = userLegacyEl;
        bool rtSubscriberOnly = false;
        string? retweetedBy = null;

        if (isRetweet)
        {
            var rtResultEl = DeepGet(legacy, "retweeted_status_result", "result");
            if (rtResultEl.HasValue)
            {
                var (rtData, rtSub) = UnwrapVisibility(rtResultEl.Value);
                rtSubscriberOnly = rtSub;
                if (rtData.TryGetProperty("legacy", out var rtLegacy) && rtLegacy.ValueKind == JsonValueKind.Object
                    && rtData.TryGetProperty("core", out var rtCore) && rtCore.ValueKind == JsonValueKind.Object)
                {
                    actualData = rtData;
                    actualLegacy = rtLegacy;
                    var rtUserResult = DeepGet(rtCore, "user_results", "result");
                    if (rtUserResult.HasValue)
                    {
                        actualUser = rtUserResult.Value;
                        actualUserLegacy = actualUser.TryGetProperty("legacy", out var aul) ? aul : default;
                    }
                }
            }
            // Original retweeter
            var rtByScreen = userCoreEl.ValueKind == JsonValueKind.Object && userCoreEl.TryGetProperty("screen_name", out var rsn)
                ? rsn.GetString()
                : (userLegacyEl.ValueKind == JsonValueKind.Object && userLegacyEl.TryGetProperty("screen_name", out var lsn2)
                    ? lsn2.GetString() : "unknown");
            retweetedBy = rtByScreen;
        }

        var media = ExtractMedia(actualLegacy);

        var urls = new List<string>();
        var urlsArr = DeepGet(actualLegacy, "entities", "urls");
        if (urlsArr?.ValueKind == JsonValueKind.Array)
            foreach (var u in urlsArr.Value.EnumerateArray())
                if (u.TryGetProperty("expanded_url", out var eu))
                    urls.Add(eu.GetString() ?? "");

        Tweet? quotedTweet = null;
        var quotedEl = DeepGet(actualData, "quoted_status_result", "result");
        if (quotedEl.HasValue)
            quotedTweet = ParseTweetResult(quotedEl.Value, depth + 1);

        var author = ExtractAuthor(
            actualUser.ValueKind == JsonValueKind.Undefined ? default : actualUser,
            actualUserLegacy.ValueKind == JsonValueKind.Undefined ? default : actualUserLegacy);

        // Prefer note_tweet full text
        var noteText = DeepGet(actualData, "note_tweet", "note_tweet_results", "result", "text");
        var text = noteText?.ValueKind == JsonValueKind.String
            ? noteText.Value.GetString() ?? ""
            : (actualLegacy.TryGetProperty("full_text", out var ft) ? ft.GetString() ?? "" : "");

        return new Tweet(
            Id: actualData.TryGetProperty("rest_id", out var restId) ? restId.GetString() ?? "" : "",
            Text: text,
            Author: author,
            Metrics: new Metrics(
                Likes: ParseInt(actualLegacy.TryGetProperty("favorite_count", out var fav) ? fav : default),
                Retweets: ParseInt(actualLegacy.TryGetProperty("retweet_count", out var rt) ? rt : default),
                Replies: ParseInt(actualLegacy.TryGetProperty("reply_count", out var rp) ? rp : default),
                Quotes: ParseInt(actualLegacy.TryGetProperty("quote_count", out var qc) ? qc : default),
                Views: ParseInt(DeepGet(actualData, "views", "count")),
                Bookmarks: ParseInt(actualLegacy.TryGetProperty("bookmark_count", out var bc) ? bc : default)
            ),
            CreatedAt: actualLegacy.TryGetProperty("created_at", out var cat) ? cat.GetString() ?? "" : "",
            Media: media,
            Urls: urls,
            IsRetweet: isRetweet,
            RetweetedBy: retweetedBy,
            QuotedTweet: quotedTweet,
            Lang: actualLegacy.TryGetProperty("lang", out var lang) ? lang.GetString() ?? "" : "",
            IsSubscriberOnly: isSubscriberOnly || rtSubscriberOnly
        );
    }

    // ── Timeline response parsing ─────────────────────────────────────────

    public static (List<Tweet> Tweets, string? NextCursor) ParseTimelineResponse(
        JsonElement data, Func<JsonElement, JsonElement?> getInstructions)
    {
        var tweets = new List<Tweet>();
        string? nextCursor = null;

        var instructions = getInstructions(data);
        if (instructions is null || instructions.Value.ValueKind != JsonValueKind.Array)
            return (tweets, nextCursor);

        foreach (var instruction in instructions.Value.EnumerateArray())
        {
            IEnumerable<JsonElement> entries;
            if (instruction.TryGetProperty("entries", out var entriesEl))
                entries = entriesEl.EnumerateArray();
            else if (instruction.TryGetProperty("moduleItems", out var moduleItems))
                entries = moduleItems.EnumerateArray();
            else
                entries = Enumerable.Empty<JsonElement>();

            foreach (var entry in entries)
            {
                if (!entry.TryGetProperty("content", out var content)) continue;
                nextCursor = ExtractCursor(content) ?? nextCursor;

                if (content.TryGetProperty("itemContent", out var itemContent))
                {
                    var result = DeepGet(itemContent, "tweet_results", "result");
                    if (result.HasValue)
                    {
                        var tweet = ParseTweetResult(result.Value);
                        if (tweet is not null) tweets.Add(tweet);
                    }
                }

                if (content.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                    foreach (var nestedItem in items.EnumerateArray())
                    {
                        var nestedResult = DeepGet(nestedItem, "item", "itemContent", "tweet_results", "result");
                        if (nestedResult.HasValue)
                        {
                            var tweet = ParseTweetResult(nestedResult.Value);
                            if (tweet is not null) tweets.Add(tweet);
                        }
                    }
            }
        }

        return (tweets, nextCursor);
    }
}
