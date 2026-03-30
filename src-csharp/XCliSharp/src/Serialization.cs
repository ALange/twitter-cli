// Serialization helpers for XCliSharp.
// Mirrors twitter_cli/serialization.py

using System.Text.Json;
using System.Text.Json.Nodes;

namespace XCliSharp;

public static class Serialization
{
    public static JsonObject TweetToDict(Tweet tweet)
    {
        var obj = new JsonObject
        {
            ["id"] = tweet.Id,
            ["text"] = tweet.Text,
            ["author"] = new JsonObject
            {
                ["id"] = tweet.Author.Id,
                ["name"] = tweet.Author.Name,
                ["screenName"] = tweet.Author.ScreenName,
                ["profileImageUrl"] = tweet.Author.ProfileImageUrl,
                ["verified"] = tweet.Author.Verified,
            },
            ["metrics"] = new JsonObject
            {
                ["likes"] = tweet.Metrics.Likes,
                ["retweets"] = tweet.Metrics.Retweets,
                ["replies"] = tweet.Metrics.Replies,
                ["quotes"] = tweet.Metrics.Quotes,
                ["views"] = tweet.Metrics.Views,
                ["bookmarks"] = tweet.Metrics.Bookmarks,
            },
            ["createdAt"] = tweet.CreatedAt,
            ["createdAtLocal"] = TimeUtil.FormatLocalTime(tweet.CreatedAt),
            ["createdAtISO"] = TimeUtil.FormatIso8601(tweet.CreatedAt),
            ["isRetweet"] = tweet.IsRetweet,
            ["retweetedBy"] = tweet.RetweetedBy,
            ["lang"] = tweet.Lang,
            ["score"] = tweet.Score.HasValue ? JsonValue.Create(tweet.Score.Value) : null,
            ["isSubscriberOnly"] = tweet.IsSubscriberOnly,
        };

        var mediaArr = new JsonArray();
        foreach (var m in tweet.Media ?? new List<TweetMedia>())
            mediaArr.Add(new JsonObject { ["type"] = m.Type, ["url"] = m.Url, ["width"] = m.Width, ["height"] = m.Height });
        obj["media"] = mediaArr;

        var urlsArr = new JsonArray();
        foreach (var u in tweet.Urls ?? new List<string>())
            urlsArr.Add(u);
        obj["urls"] = urlsArr;

        if (tweet.ArticleTitle is not null) obj["articleTitle"] = tweet.ArticleTitle;
        if (tweet.ArticleText is not null) obj["articleText"] = tweet.ArticleText;

        if (tweet.QuotedTweet is not null)
        {
            obj["quotedTweet"] = new JsonObject
            {
                ["id"] = tweet.QuotedTweet.Id,
                ["text"] = tweet.QuotedTweet.Text,
                ["author"] = new JsonObject
                {
                    ["screenName"] = tweet.QuotedTweet.Author.ScreenName,
                    ["name"] = tweet.QuotedTweet.Author.Name,
                },
            };
        }

        return obj;
    }

    public static string TweetsToJson(IEnumerable<Tweet> tweets)
    {
        var arr = new JsonArray();
        foreach (var t in tweets)
            arr.Add(TweetToDict(t));
        // Use WriteTo with Utf8JsonWriter to avoid JsonSerializerOptions TypeInfoResolver requirement
        using var ms = new System.IO.MemoryStream();
        using var writer = new System.Text.Json.Utf8JsonWriter(ms, new System.Text.Json.JsonWriterOptions { Indented = true });
        arr.WriteTo(writer);
        writer.Flush();
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    public static JsonObject UserProfileToDict(UserProfile user)
    {
        return new JsonObject
        {
            ["id"] = user.Id,
            ["name"] = user.Name,
            ["screenName"] = user.ScreenName,
            ["bio"] = user.Bio,
            ["location"] = user.Location,
            ["url"] = user.Url,
            ["followersCount"] = user.FollowersCount,
            ["followingCount"] = user.FollowingCount,
            ["tweetsCount"] = user.TweetsCount,
            ["likesCount"] = user.LikesCount,
            ["verified"] = user.Verified,
            ["profileImageUrl"] = user.ProfileImageUrl,
            ["createdAt"] = user.CreatedAt,
        };
    }

    public static Tweet TweetFromDict(JsonElement data)
    {
        var authorData = data.TryGetProperty("author", out var a) ? a : default;
        var metricsData = data.TryGetProperty("metrics", out var m) ? m : default;

        Tweet? quotedTweet = null;
        if (data.TryGetProperty("quotedTweet", out var qt) && qt.ValueKind == JsonValueKind.Object)
        {
            var qAuthor = qt.TryGetProperty("author", out var qa) ? qa : default;
            quotedTweet = new Tweet(
                Id: qt.TryGetProperty("id", out var qid) ? qid.GetString() ?? "" : "",
                Text: qt.TryGetProperty("text", out var qtxt) ? qtxt.GetString() ?? "" : "",
                Author: new Author(
                    Id: "",
                    Name: qAuthor.ValueKind == JsonValueKind.Object && qAuthor.TryGetProperty("name", out var qn) ? qn.GetString() ?? "" : "",
                    ScreenName: qAuthor.ValueKind == JsonValueKind.Object && qAuthor.TryGetProperty("screenName", out var qsn) ? qsn.GetString() ?? "" : ""
                ),
                Metrics: new Metrics(),
                CreatedAt: ""
            );
        }

        return new Tweet(
            Id: data.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "",
            Text: data.TryGetProperty("text", out var textProp) ? textProp.GetString() ?? "" : "",
            Author: new Author(
                Id: authorData.ValueKind != JsonValueKind.Undefined && authorData.TryGetProperty("id", out var aid) ? aid.GetString() ?? "" : "",
                Name: authorData.ValueKind != JsonValueKind.Undefined && authorData.TryGetProperty("name", out var aname) ? aname.GetString() ?? "" : "",
                ScreenName: authorData.ValueKind != JsonValueKind.Undefined && authorData.TryGetProperty("screenName", out var asn) ? asn.GetString() ?? "" : "",
                ProfileImageUrl: authorData.ValueKind != JsonValueKind.Undefined && authorData.TryGetProperty("profileImageUrl", out var apiu) ? apiu.GetString() ?? "" : "",
                Verified: authorData.ValueKind != JsonValueKind.Undefined && authorData.TryGetProperty("verified", out var av) && av.GetBoolean()
            ),
            Metrics: new Metrics(
                Likes: metricsData.ValueKind != JsonValueKind.Undefined && metricsData.TryGetProperty("likes", out var ml) ? ml.GetInt32() : 0,
                Retweets: metricsData.ValueKind != JsonValueKind.Undefined && metricsData.TryGetProperty("retweets", out var mr) ? mr.GetInt32() : 0,
                Replies: metricsData.ValueKind != JsonValueKind.Undefined && metricsData.TryGetProperty("replies", out var mrl) ? mrl.GetInt32() : 0,
                Quotes: metricsData.ValueKind != JsonValueKind.Undefined && metricsData.TryGetProperty("quotes", out var mq) ? mq.GetInt32() : 0,
                Views: metricsData.ValueKind != JsonValueKind.Undefined && metricsData.TryGetProperty("views", out var mv) ? mv.GetInt32() : 0,
                Bookmarks: metricsData.ValueKind != JsonValueKind.Undefined && metricsData.TryGetProperty("bookmarks", out var mb) ? mb.GetInt32() : 0
            ),
            CreatedAt: data.TryGetProperty("createdAt", out var cat) ? cat.GetString() ?? "" : "",
            IsRetweet: data.TryGetProperty("isRetweet", out var ir) && ir.GetBoolean(),
            Lang: data.TryGetProperty("lang", out var lang) ? lang.GetString() ?? "" : "",
            RetweetedBy: data.TryGetProperty("retweetedBy", out var rb) && rb.ValueKind == JsonValueKind.String ? rb.GetString() : null,
            Score: data.TryGetProperty("score", out var sc) && sc.ValueKind == JsonValueKind.Number ? sc.GetDouble() : null,
            QuotedTweet: quotedTweet,
            IsSubscriberOnly: data.TryGetProperty("isSubscriberOnly", out var iso) && iso.GetBoolean()
        );
    }
}
