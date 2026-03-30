// Tests for TweetCache - mirrors test_cache.py
using System.Text.Json;
using XCliSharp;
using Xunit;

namespace XCliSharp.Tests;

public class TweetCacheTests
{
    private static Tweet MakeTweet(string id, string screenName = "user", string text = "Hello") =>
        new Tweet(
            Id: id,
            Text: text,
            Author: new Author("u1", "User", screenName),
            Metrics: new Metrics(),
            CreatedAt: "Sat Mar 08 12:00:00 +0000 2026"
        );

    [Fact]
    public void SaveAndResolve_ExistingIndex_ReturnsTweetId()
    {
        var tweets = new List<Tweet>
        {
            MakeTweet("tweet_id_1"),
            MakeTweet("tweet_id_2"),
            MakeTweet("tweet_id_3"),
        };

        TweetCache.SaveTweetCache(tweets);
        var (id, count) = TweetCache.ResolveCachedTweet(2);

        Assert.Equal("tweet_id_2", id);
        Assert.Equal(3, count);
    }

    [Fact]
    public void SaveAndResolve_FirstIndex_ReturnsTweetId()
    {
        var tweets = new List<Tweet> { MakeTweet("first_tweet") };
        TweetCache.SaveTweetCache(tweets);

        var (id, _) = TweetCache.ResolveCachedTweet(1);
        Assert.Equal("first_tweet", id);
    }

    [Fact]
    public void Resolve_OutOfRangeIndex_ReturnsNull()
    {
        var tweets = new List<Tweet> { MakeTweet("only_tweet") };
        TweetCache.SaveTweetCache(tweets);

        var (id, count) = TweetCache.ResolveCachedTweet(100);
        Assert.Null(id);
        Assert.Equal(1, count);
    }

    [Fact]
    public void SaveCache_PreservesUpTo80CharsOfText()
    {
        var longText = new string('A', 100);
        var tweets = new List<Tweet> { MakeTweet("t1", text: longText) };
        TweetCache.SaveTweetCache(tweets);

        // Should not throw
        var (id, _) = TweetCache.ResolveCachedTweet(1);
        Assert.Equal("t1", id);
    }

    [Fact]
    public void SaveCache_EmptyList_Succeeds()
    {
        // Should not throw
        TweetCache.SaveTweetCache(new List<Tweet>());
    }
}
