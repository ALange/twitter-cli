// Tests for Parser - mirrors test_parser_fixtures.py
using System.Text.Json;
using XCliSharp;
using Xunit;

namespace XCliSharp.Tests;

public class ParserTests
{
    private static JsonElement ParseJson(string json) => JsonDocument.Parse(json).RootElement;

    // ── DeepGet tests ─────────────────────────────────────────────────────

    [Fact]
    public void DeepGet_SingleKey_ReturnsValue()
    {
        var obj = ParseJson("{\"foo\": 42}");
        var result = Parser.DeepGet(obj, "foo");
        Assert.NotNull(result);
        Assert.Equal(42, result.Value.GetInt32());
    }

    [Fact]
    public void DeepGet_NestedKey_ReturnsNestedValue()
    {
        var obj = ParseJson("{\"a\": {\"b\": {\"c\": \"hello\"}}}");
        var result = Parser.DeepGet(obj, "a", "b", "c");
        Assert.NotNull(result);
        Assert.Equal("hello", result.Value.GetString());
    }

    [Fact]
    public void DeepGet_MissingKey_ReturnsNull()
    {
        var obj = ParseJson("{\"foo\": 42}");
        var result = Parser.DeepGet(obj, "bar");
        Assert.Null(result);
    }

    [Fact]
    public void DeepGet_PartialPath_ReturnsNull()
    {
        var obj = ParseJson("{\"a\": {\"b\": 1}}");
        var result = Parser.DeepGet(obj, "a", "b", "c");
        Assert.Null(result);
    }

    // ── ParseInt tests ────────────────────────────────────────────────────

    [Fact]
    public void ParseInt_Number_ReturnsNumber()
    {
        var el = ParseJson("42");
        Assert.Equal(42, Parser.ParseInt(el));
    }

    [Fact]
    public void ParseInt_StringNumber_ReturnsNumber()
    {
        var el = ParseJson("\"100\"");
        Assert.Equal(100, Parser.ParseInt(el));
    }

    [Fact]
    public void ParseInt_Null_ReturnsDefault()
    {
        Assert.Equal(0, Parser.ParseInt(null));
        Assert.Equal(5, Parser.ParseInt(null, 5));
    }

    // ── ParseUserResult tests ─────────────────────────────────────────────

    private static string UserResultJson(string screenName = "testuser", string name = "Test User",
        int followers = 1000, bool verified = false) => $$"""
        {
            "__typename": "User",
            "rest_id": "12345",
            "is_blue_verified": {{(verified ? "true" : "false")}},
            "legacy": {
                "name": "{{name}}",
                "screen_name": "{{screenName}}",
                "description": "Test bio",
                "location": "NYC",
                "followers_count": {{followers}},
                "friends_count": 200,
                "statuses_count": 5000,
                "favourites_count": 10000,
                "profile_image_url_https": "https://pbs.twimg.com/profile_images/test/photo.jpg",
                "created_at": "Mon Jan 01 00:00:00 +0000 2020"
            }
        }
        """;

    [Fact]
    public void ParseUserResult_ValidData_ReturnsUserProfile()
    {
        var userData = ParseJson(UserResultJson());
        var user = Parser.ParseUserResult(userData);

        Assert.NotNull(user);
        Assert.Equal("12345", user!.Id);
        Assert.Equal("testuser", user.ScreenName);
        Assert.Equal("Test User", user.Name);
        Assert.Equal("Test bio", user.Bio);
        Assert.Equal("NYC", user.Location);
        Assert.Equal(1000, user.FollowersCount);
        Assert.Equal(200, user.FollowingCount);
        Assert.Equal(5000, user.TweetsCount);
    }

    [Fact]
    public void ParseUserResult_Unavailable_ReturnsNull()
    {
        var userData = ParseJson("{\"__typename\": \"UserUnavailable\"}");
        var user = Parser.ParseUserResult(userData);
        Assert.Null(user);
    }

    [Fact]
    public void ParseUserResult_NoLegacy_ReturnsNull()
    {
        var userData = ParseJson("{\"rest_id\": \"12345\"}");
        var user = Parser.ParseUserResult(userData);
        Assert.Null(user);
    }

    [Fact]
    public void ParseUserResult_Verified_SetsVerifiedTrue()
    {
        var userData = ParseJson(UserResultJson(verified: true));
        var user = Parser.ParseUserResult(userData);
        Assert.NotNull(user);
        Assert.True(user!.Verified);
    }

    // ── ParseTweetResult tests ────────────────────────────────────────────

    private static string TweetResultJson(
        string id = "tweet123",
        string text = "Hello world",
        string lang = "en",
        int likes = 42,
        int retweets = 10) => $$"""
        {
            "__typename": "Tweet",
            "rest_id": "{{id}}",
            "core": {
                "user_results": {
                    "result": {
                        "rest_id": "user1",
                        "core": {
                            "name": "Test User",
                            "screen_name": "testuser"
                        },
                        "legacy": {
                            "name": "Test User",
                            "screen_name": "testuser",
                            "profile_image_url_https": ""
                        }
                    }
                }
            },
            "legacy": {
                "full_text": "{{text}}",
                "lang": "{{lang}}",
                "favorite_count": {{likes}},
                "retweet_count": {{retweets}},
                "reply_count": 5,
                "quote_count": 2,
                "bookmark_count": 1,
                "created_at": "Sat Mar 08 12:00:00 +0000 2026"
            },
            "views": {
                "count": "1000"
            }
        }
        """;

    [Fact]
    public void ParseTweetResult_ValidData_ReturnsTweet()
    {
        var result = ParseJson(TweetResultJson());
        var tweet = Parser.ParseTweetResult(result);

        Assert.NotNull(tweet);
        Assert.Equal("tweet123", tweet!.Id);
        Assert.Equal("Hello world", tweet.Text);
        Assert.Equal("en", tweet.Lang);
        Assert.Equal(42, tweet.Metrics.Likes);
        Assert.Equal(10, tweet.Metrics.Retweets);
        Assert.Equal(5, tweet.Metrics.Replies);
        Assert.Equal(1000, tweet.Metrics.Views);
        Assert.Equal("testuser", tweet.Author.ScreenName);
    }

    [Fact]
    public void ParseTweetResult_Tombstone_ReturnsNull()
    {
        var result = ParseJson("{\"__typename\": \"TweetTombstone\"}");
        var tweet = Parser.ParseTweetResult(result);
        Assert.Null(tweet);
    }

    [Fact]
    public void ParseTweetResult_NoLegacy_ReturnsNull()
    {
        var result = ParseJson("{\"rest_id\": \"123\", \"core\": {}}");
        var tweet = Parser.ParseTweetResult(result);
        Assert.Null(tweet);
    }

    [Fact]
    public void ParseTweetResult_TweetWithVisibilityResults_UnwrapsInner()
    {
        var json = $$"""
        {
            "__typename": "TweetWithVisibilityResults",
            "tweet": {{TweetResultJson(id: "visible_tweet")}}
        }
        """;
        var result = ParseJson(json);
        var tweet = Parser.ParseTweetResult(result);

        Assert.NotNull(tweet);
        Assert.Equal("visible_tweet", tweet!.Id);
    }

    // ── ParseTimelineResponse tests ───────────────────────────────────────

    [Fact]
    public void ParseTimelineResponse_ValidResponse_ExtractsTweets()
    {
        var json = $$"""
        {
            "data": {
                "timeline": {
                    "instructions": [
                        {
                            "entries": [
                                {
                                    "content": {
                                        "itemContent": {
                                            "tweet_results": {
                                                "result": {{TweetResultJson(id: "timeline_tweet1")}}
                                            }
                                        }
                                    }
                                },
                                {
                                    "content": {
                                        "itemContent": {
                                            "tweet_results": {
                                                "result": {{TweetResultJson(id: "timeline_tweet2")}}
                                            }
                                        }
                                    }
                                }
                            ]
                        }
                    ]
                }
            }
        }
        """;

        var data = ParseJson(json);
        var (tweets, cursor) = Parser.ParseTimelineResponse(
            data,
            d => Parser.DeepGet(d, "data", "timeline", "instructions"));

        Assert.Equal(2, tweets.Count);
        Assert.Null(cursor);
        Assert.Equal("timeline_tweet1", tweets[0].Id);
        Assert.Equal("timeline_tweet2", tweets[1].Id);
    }

    [Fact]
    public void ParseTimelineResponse_WithCursor_ExtractsCursor()
    {
        var json = """
        {
            "data": {
                "instructions": [
                    {
                        "entries": [
                            {
                                "content": {
                                    "cursorType": "Bottom",
                                    "value": "next_page_cursor_abc"
                                }
                            }
                        ]
                    }
                ]
            }
        }
        """;

        var data = ParseJson(json);
        var (tweets, cursor) = Parser.ParseTimelineResponse(
            data,
            d => Parser.DeepGet(d, "data", "instructions"));

        Assert.Empty(tweets);
        Assert.Equal("next_page_cursor_abc", cursor);
    }

    [Fact]
    public void ParseTimelineResponse_EmptyInstructions_ReturnsEmpty()
    {
        var data = ParseJson("{}");
        var (tweets, cursor) = Parser.ParseTimelineResponse(data, _ => null);

        Assert.Empty(tweets);
        Assert.Null(cursor);
    }
}
