// Tests for Serialization - mirrors test_serialization.py
using System.Text.Json;
using System.Text.Json.Nodes;
using XCliSharp;
using Xunit;

namespace XCliSharp.Tests;

public class SerializationTests
{
    private static Tweet MakeTweet(
        string id = "123",
        string text = "Hello world",
        bool isRetweet = false,
        string lang = "en") => new Tweet(
        Id: id,
        Text: text,
        Author: new Author("u1", "Test User", "testuser", "https://example.com/photo.jpg", false),
        Metrics: new Metrics(Likes: 42, Retweets: 10, Replies: 5, Quotes: 2, Views: 1000, Bookmarks: 3),
        CreatedAt: "Sat Mar 08 12:00:00 +0000 2026",
        Media: new List<TweetMedia> { new TweetMedia("photo", "https://example.com/image.jpg", 1280, 720) },
        Urls: new List<string> { "https://example.com" },
        IsRetweet: isRetweet,
        Lang: lang
    );

    [Fact]
    public void TweetToDict_BasicFields_Correct()
    {
        var tweet = MakeTweet();
        var dict = Serialization.TweetToDict(tweet);

        Assert.Equal("123", dict["id"]?.ToString());
        Assert.Equal("Hello world", dict["text"]?.ToString());
        Assert.False((bool)dict["isRetweet"]!);
        Assert.Equal("en", dict["lang"]?.ToString());
    }

    [Fact]
    public void TweetToDict_AuthorFields_Correct()
    {
        var tweet = MakeTweet();
        var dict = Serialization.TweetToDict(tweet);

        var author = dict["author"]!.AsObject();
        Assert.Equal("u1", author["id"]?.ToString());
        Assert.Equal("testuser", author["screenName"]?.ToString());
        Assert.Equal("Test User", author["name"]?.ToString());
    }

    [Fact]
    public void TweetToDict_MetricsFields_Correct()
    {
        var tweet = MakeTweet();
        var dict = Serialization.TweetToDict(tweet);

        var metrics = dict["metrics"]!.AsObject();
        Assert.Equal("42", metrics["likes"]?.ToString());
        Assert.Equal("10", metrics["retweets"]?.ToString());
        Assert.Equal("5", metrics["replies"]?.ToString());
        Assert.Equal("1000", metrics["views"]?.ToString());
    }

    [Fact]
    public void TweetToDict_MediaList_Correct()
    {
        var tweet = MakeTweet();
        var dict = Serialization.TweetToDict(tweet);

        var media = dict["media"]!.AsArray();
        Assert.Single(media);
        Assert.Equal("photo", media[0]!["type"]?.ToString());
        Assert.Equal("https://example.com/image.jpg", media[0]!["url"]?.ToString());
    }

    [Fact]
    public void TweetToDict_IncludesCreatedAtVariants()
    {
        var tweet = MakeTweet();
        var dict = Serialization.TweetToDict(tweet);

        Assert.Contains("createdAt", dict.Select(kv => kv.Key));
        Assert.Contains("createdAtLocal", dict.Select(kv => kv.Key));
        Assert.Contains("createdAtISO", dict.Select(kv => kv.Key));
    }

    [Fact]
    public void TweetToDict_WithQuotedTweet_IncludesQuotedTweet()
    {
        var quoted = MakeTweet("q1", "Quoted text");
        var tweet = MakeTweet() with { QuotedTweet = quoted };
        var dict = Serialization.TweetToDict(tweet);

        Assert.Contains("quotedTweet", dict.Select(kv => kv.Key));
        var qt = dict["quotedTweet"]!.AsObject();
        Assert.Equal("q1", qt["id"]?.ToString());
    }

    [Fact]
    public void TweetsToJson_MultipleItems_IsValidJsonArray()
    {
        var tweets = new List<Tweet> { MakeTweet("1"), MakeTweet("2") };
        var json = Serialization.TweetsToJson(tweets);

        var arr = JsonDocument.Parse(json).RootElement;
        Assert.Equal(JsonValueKind.Array, arr.ValueKind);
        Assert.Equal(2, arr.GetArrayLength());
    }

    [Fact]
    public void UserProfileToDict_BasicFields_Correct()
    {
        var user = new UserProfile(
            Id: "u1",
            Name: "Test User",
            ScreenName: "testuser",
            Bio: "A test user",
            Location: "NYC",
            FollowersCount: 1000,
            FollowingCount: 200,
            TweetsCount: 5000,
            Verified: true
        );
        var dict = Serialization.UserProfileToDict(user);

        Assert.Equal("u1", dict["id"]?.ToString());
        Assert.Equal("testuser", dict["screenName"]?.ToString());
        Assert.Equal("Test User", dict["name"]?.ToString());
        Assert.Equal("A test user", dict["bio"]?.ToString());
        Assert.Equal("NYC", dict["location"]?.ToString());
        Assert.Equal("1000", dict["followersCount"]?.ToString());
        Assert.True((bool)dict["verified"]!);
    }

    [Fact]
    public void TweetFromDict_RoundTrip_PreservesFields()
    {
        var original = MakeTweet("roundtrip1", "Round trip test");
        var dict = Serialization.TweetToDict(original);
        var json = dict.ToJsonString();

        var parsed = JsonDocument.Parse(json).RootElement;
        var restored = Serialization.TweetFromDict(parsed);

        Assert.Equal("roundtrip1", restored.Id);
        Assert.Equal("Round trip test", restored.Text);
        Assert.Equal("testuser", restored.Author.ScreenName);
        Assert.Equal(42, restored.Metrics.Likes);
        Assert.Equal(10, restored.Metrics.Retweets);
    }
}
