// Tests for TweetFilter - mirrors test_filter.py
using XCliSharp;
using Xunit;

namespace XCliSharp.Tests;

public class TweetFilterTests
{
    private static Tweet MakeTweet(
        string id = "1",
        int likes = 100,
        int retweets = 50,
        int replies = 20,
        int views = 10000,
        int bookmarks = 5,
        string lang = "en",
        bool isRetweet = false)
    {
        return new Tweet(
            Id: id,
            Text: $"Test tweet {id}",
            Author: new Author("u1", "Test User", "testuser"),
            Metrics: new Metrics(likes, retweets, replies, 0, views, bookmarks),
            CreatedAt: "Sat Mar 08 12:00:00 +0000 2026",
            Lang: lang,
            IsRetweet: isRetweet
        );
    }

    [Fact]
    public void ScoreTweet_DefaultWeights_ReturnsCorrectScore()
    {
        var tweet = MakeTweet(likes: 100, retweets: 10, replies: 5, views: 1000, bookmarks: 0);
        var score = TweetFilter.ScoreTweet(tweet);
        // score = 1.0*100 + 3.0*10 + 2.0*5 + 5.0*0 + 0.5*log10(1000)
        //       = 100 + 30 + 10 + 0 + 0.5*3 = 141.5
        Assert.Equal(141.5, score, precision: 5);
    }

    [Fact]
    public void ScoreTweet_ZeroViews_UsesMinimumOneForLog()
    {
        var tweet = MakeTweet(likes: 0, retweets: 0, replies: 0, views: 0, bookmarks: 0);
        var score = TweetFilter.ScoreTweet(tweet);
        // 0.5 * log10(1) = 0
        Assert.Equal(0.0, score, precision: 5);
    }

    [Fact]
    public void ScoreTweet_CustomWeights_UsesCustomWeights()
    {
        var tweet = MakeTweet(likes: 100, retweets: 0, replies: 0, views: 0, bookmarks: 0);
        var weights = new Dictionary<string, double> { ["likes"] = 2.0, ["retweets"] = 0, ["replies"] = 0, ["bookmarks"] = 0, ["views_log"] = 0 };
        var score = TweetFilter.ScoreTweet(tweet, weights);
        Assert.Equal(200.0, score, precision: 5);
    }

    [Fact]
    public void FilterTweets_TopNMode_ReturnsTopN()
    {
        var tweets = Enumerable.Range(1, 10)
            .Select(i => MakeTweet(id: i.ToString(), likes: i * 10))
            .ToList();

        var config = new FilterConfig(Mode: FilterMode.TopN, TopN: 3);
        var result = TweetFilter.FilterTweets(tweets, config);

        Assert.Equal(3, result.Count);
        // Should be sorted by score descending (tweet 10 has highest likes)
        Assert.Equal("10", result[0].Id);
    }

    [Fact]
    public void FilterTweets_ScoreMode_ReturnsAboveMinScore()
    {
        var tweets = new[]
        {
            MakeTweet("1", likes: 1000),   // high score
            MakeTweet("2", likes: 1),      // low score
        };

        var config = new FilterConfig(Mode: FilterMode.Score, MinScore: 500.0);
        var result = TweetFilter.FilterTweets(tweets, config);

        Assert.Single(result);
        Assert.Equal("1", result[0].Id);
    }

    [Fact]
    public void FilterTweets_AllMode_ReturnsAllTweets()
    {
        var tweets = Enumerable.Range(1, 5)
            .Select(i => MakeTweet(id: i.ToString()))
            .ToList();

        var config = new FilterConfig(Mode: FilterMode.All);
        var result = TweetFilter.FilterTweets(tweets, config);

        Assert.Equal(5, result.Count);
    }

    [Fact]
    public void FilterTweets_LangFilter_FiltersCorrectly()
    {
        var tweets = new[]
        {
            MakeTweet("1", lang: "en"),
            MakeTweet("2", lang: "fr"),
            MakeTweet("3", lang: "en"),
        };

        var config = new FilterConfig(Mode: FilterMode.All, Lang: new List<string> { "en" });
        var result = TweetFilter.FilterTweets(tweets, config);

        Assert.Equal(2, result.Count);
        Assert.All(result, t => Assert.Equal("en", t.Lang));
    }

    [Fact]
    public void FilterTweets_ExcludeRetweets_FiltersCorrectly()
    {
        var tweets = new[]
        {
            MakeTweet("1", isRetweet: false),
            MakeTweet("2", isRetweet: true),
            MakeTweet("3", isRetweet: false),
        };

        var config = new FilterConfig(Mode: FilterMode.All, ExcludeRetweets: true);
        var result = TweetFilter.FilterTweets(tweets, config);

        Assert.Equal(2, result.Count);
        Assert.All(result, t => Assert.False(t.IsRetweet));
    }

    [Fact]
    public void FilterTweets_SortsByScoreDescending()
    {
        var tweets = new[]
        {
            MakeTweet("1", likes: 10),
            MakeTweet("2", likes: 100),
            MakeTweet("3", likes: 50),
        };

        var config = new FilterConfig(Mode: FilterMode.All);
        var result = TweetFilter.FilterTweets(tweets, config);

        // Should be 2, 3, 1 by score
        Assert.Equal("2", result[0].Id);
        Assert.Equal("3", result[1].Id);
        Assert.Equal("1", result[2].Id);
    }

    [Fact]
    public void FilterTweets_SetsScoreOnTweets()
    {
        var tweets = new[] { MakeTweet("1", likes: 100) };
        var config = new FilterConfig(Mode: FilterMode.All);
        var result = TweetFilter.FilterTweets(tweets, config);

        Assert.NotNull(result[0].Score);
        Assert.True(result[0].Score > 0);
    }

    [Fact]
    public void FilterTweets_EmptyList_ReturnsEmpty()
    {
        var result = TweetFilter.FilterTweets(new List<Tweet>(), new FilterConfig());
        Assert.Empty(result);
    }
}
