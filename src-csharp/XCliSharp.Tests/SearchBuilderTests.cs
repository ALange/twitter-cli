// Tests for SearchBuilder - mirrors test_search.py
using XCliSharp;
using Xunit;

namespace XCliSharp.Tests;

public class SearchBuilderTests
{
    [Fact]
    public void BuildSearchQuery_QueryOnly_ReturnsQuery()
    {
        var result = SearchBuilder.BuildSearchQuery("hello world");
        Assert.Equal("hello world", result);
    }

    [Fact]
    public void BuildSearchQuery_WithFromUser_AddsFromOperator()
    {
        var result = SearchBuilder.BuildSearchQuery("test", fromUser: "jack");
        Assert.Equal("test from:jack", result);
    }

    [Fact]
    public void BuildSearchQuery_FromUserWithAt_StripsAt()
    {
        var result = SearchBuilder.BuildSearchQuery("test", fromUser: "@jack");
        Assert.Equal("test from:jack", result);
    }

    [Fact]
    public void BuildSearchQuery_WithToUser_AddsToOperator()
    {
        var result = SearchBuilder.BuildSearchQuery("", toUser: "elonmusk");
        Assert.Equal("to:elonmusk", result);
    }

    [Fact]
    public void BuildSearchQuery_WithLang_AddsLangOperator()
    {
        var result = SearchBuilder.BuildSearchQuery("AI", lang: "en");
        Assert.Equal("AI lang:en", result);
    }

    [Fact]
    public void BuildSearchQuery_WithSinceUntil_AddsDateOperators()
    {
        var result = SearchBuilder.BuildSearchQuery("news", since: "2026-01-01", until: "2026-12-31");
        Assert.Contains("since:2026-01-01", result);
        Assert.Contains("until:2026-12-31", result);
    }

    [Fact]
    public void BuildSearchQuery_WithHas_AddsFilterOperator()
    {
        var result = SearchBuilder.BuildSearchQuery("", has: new[] { "images", "links" });
        Assert.Contains("filter:images", result);
        Assert.Contains("filter:links", result);
    }

    [Fact]
    public void BuildSearchQuery_WithExcludeRetweets_AddsNegativeFilter()
    {
        var result = SearchBuilder.BuildSearchQuery("", exclude: new[] { "retweets" });
        Assert.Contains("-filter:retweets", result);
    }

    [Fact]
    public void BuildSearchQuery_WithMinLikes_AddsMinFaves()
    {
        var result = SearchBuilder.BuildSearchQuery("", minLikes: 100);
        Assert.Contains("min_faves:100", result);
    }

    [Fact]
    public void BuildSearchQuery_WithMinRetweets_AddsMinRetweets()
    {
        var result = SearchBuilder.BuildSearchQuery("", minRetweets: 50);
        Assert.Contains("min_retweets:50", result);
    }

    [Fact]
    public void BuildSearchQuery_EmptyQuery_ReturnsEmptyString()
    {
        var result = SearchBuilder.BuildSearchQuery();
        Assert.Equal("", result);
    }

    [Fact]
    public void BuildSearchQuery_InvalidLang_ThrowsInvalidInputException()
    {
        Assert.Throws<InvalidInputException>(() =>
            SearchBuilder.BuildSearchQuery("test", lang: "not valid lang 123!@#"));
    }

    [Fact]
    public void BuildSearchQuery_NegativeMinLikes_ThrowsInvalidInputException()
    {
        Assert.Throws<InvalidInputException>(() =>
            SearchBuilder.BuildSearchQuery("test", minLikes: -1));
    }

    [Fact]
    public void BuildSearchQuery_NegativeMinRetweets_ThrowsInvalidInputException()
    {
        Assert.Throws<InvalidInputException>(() =>
            SearchBuilder.BuildSearchQuery("test", minRetweets: -1));
    }

    [Fact]
    public void BuildSearchQuery_SinceAfterUntil_ThrowsInvalidInputException()
    {
        Assert.Throws<InvalidInputException>(() =>
            SearchBuilder.BuildSearchQuery("test", since: "2026-12-31", until: "2026-01-01"));
    }

    [Fact]
    public void BuildSearchQuery_InvalidSinceDate_ThrowsInvalidInputException()
    {
        Assert.Throws<InvalidInputException>(() =>
            SearchBuilder.BuildSearchQuery("test", since: "not-a-date"));
    }

    [Fact]
    public void BuildSearchQuery_ValidZhCnLang_IsAccepted()
    {
        var result = SearchBuilder.BuildSearchQuery("test", lang: "zh-cn");
        Assert.Contains("lang:zh-cn", result);
    }

    [Fact]
    public void BuildSearchQuery_AllOptions_CombinesCorrectly()
    {
        var result = SearchBuilder.BuildSearchQuery(
            "hello",
            fromUser: "jack",
            lang: "en",
            since: "2026-01-01",
            until: "2026-06-30",
            minLikes: 10);

        Assert.StartsWith("hello", result);
        Assert.Contains("from:jack", result);
        Assert.Contains("lang:en", result);
        Assert.Contains("since:2026-01-01", result);
        Assert.Contains("until:2026-06-30", result);
        Assert.Contains("min_faves:10", result);
    }
}
