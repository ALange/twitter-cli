// Tests for McpTools - validates the MCP tool logic without a live Twitter connection
using System.Text.Json;
using XCliSharp;
using Xunit;

namespace XCliSharp.Tests;

public class McpToolsTests
{
    // ── search_tweet validation tests (don't need live credentials) ────────

    /// <summary>
    /// MCP tools return JSON errors on bad input so the LLM can see what went wrong.
    /// We can validate the input-validation path without credentials by checking the
    /// SearchBuilder layer (which is what McpTools.SearchTweet calls first).
    /// </summary>

    [Fact]
    public void SearchBuilder_EmptyQueryAndNoFilters_ProducesEmptyString()
    {
        // Verify that an empty query produces an empty string — the MCP tool
        // reports this as an error to the LLM client.
        var result = SearchBuilder.BuildSearchQuery();
        Assert.Equal("", result);
    }

    [Fact]
    public void SearchBuilder_WithFromUser_ProducesSearchQuery()
    {
        var q = SearchBuilder.BuildSearchQuery("test", fromUser: "jack");
        Assert.Equal("test from:jack", q);
    }

    [Fact]
    public void SearchBuilder_InvalidLang_ThrowsInvalidInput()
    {
        Assert.Throws<InvalidInputException>(() =>
            SearchBuilder.BuildSearchQuery("test", lang: "not!valid"));
    }

    [Fact]
    public void SearchBuilder_NegativeMinLikes_ThrowsInvalidInput()
    {
        Assert.Throws<InvalidInputException>(() =>
            SearchBuilder.BuildSearchQuery("test", minLikes: -1));
    }

    [Fact]
    public void SearchBuilder_ValidDate_Accepted()
    {
        var q = SearchBuilder.BuildSearchQuery("news", since: "2026-01-01", until: "2026-12-31");
        Assert.Contains("since:2026-01-01", q);
        Assert.Contains("until:2026-12-31", q);
    }

    [Fact]
    public void SearchBuilder_SinceAfterUntil_ThrowsInvalidInput()
    {
        Assert.Throws<InvalidInputException>(() =>
            SearchBuilder.BuildSearchQuery("test", since: "2026-12-31", until: "2026-01-01"));
    }

    // ── McpTools.SearchTweet error-return tests ────────────────────────────

    /// <summary>
    /// When an empty query (no filters at all) is passed, the MCP tool returns
    /// a JSON error object rather than throwing — callers parse it.
    /// We verify the shape without needing network access by calling SearchTweet
    /// with params that hit the early-exit validation path.
    /// </summary>
    [Fact]
    public async Task SearchTweet_EmptyQueryAndNoFilters_ReturnsJsonError()
    {
        var tools = new McpTools();
        // This will hit the SearchBuilder.BuildSearchQuery path first,
        // get "", then return JSON error before touching the network.
        // NOTE: Auth.Resolve() is NOT called because we exit early.
        var result = await tools.SearchTweet(query: "", max_results: 20);
        var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("error", out var errEl));
        Assert.False(string.IsNullOrEmpty(errEl.GetString()));
    }

    [Fact]
    public async Task SearchTweet_InvalidProduct_ReturnsJsonError()
    {
        var tools = new McpTools();
        // Invalid product returns an error before network call
        var result = await tools.SearchTweet(query: "hello", product: "BadProduct");
        var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("error", out var errEl));
        Assert.Contains("product", errEl.GetString() ?? "");
    }

    [Fact]
    public async Task SearchTweet_InvalidLang_ReturnsJsonError()
    {
        var tools = new McpTools();
        // InvalidInputException from SearchBuilder is caught and returned as JSON
        var result = await tools.SearchTweet(query: "test", lang: "not!valid");
        var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task SearchTweet_NegativeMinLikes_ReturnsJsonError()
    {
        var tools = new McpTools();
        var result = await tools.SearchTweet(query: "test", min_likes: -1);
        var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task SearchTweet_SinceAfterUntil_ReturnsJsonError()
    {
        var tools = new McpTools();
        var result = await tools.SearchTweet(query: "test", since: "2026-12-31", until: "2026-01-01");
        var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    // ── Product validation ─────────────────────────────────────────────────

    [Theory]
    [InlineData("Top")]
    [InlineData("Latest")]
    [InlineData("Photos")]
    [InlineData("Videos")]
    public async Task SearchTweet_ValidProduct_DoesNotReturnProductError(string product)
    {
        var tools = new McpTools();
        // Valid product — won't get "product must be one of:" error.
        // It will fail at Auth.Resolve() because no credentials are set.
        // That means the error message will be about authentication, not product.
        var result = await tools.SearchTweet(query: "hello world", product: product);
        var doc = JsonDocument.Parse(result);
        if (doc.RootElement.TryGetProperty("error", out var errEl))
        {
            // Should NOT be a product error
            Assert.DoesNotContain("product must be", errEl.GetString() ?? "");
        }
    }

    // ── McpTools.GetUserProfile error tests ────────────────────────────────

    [Fact]
    public async Task GetUserProfile_AuthFails_ReturnsJsonError()
    {
        // No credentials set → returns JSON error
        Environment.SetEnvironmentVariable("TWITTER_AUTH_TOKEN", null);
        Environment.SetEnvironmentVariable("TWITTER_CT0", null);

        var tools = new McpTools();
        var result = await tools.GetUserProfile("testuser");
        var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    // ── McpTools.GetHomeTimeline error tests ──────────────────────────────

    [Fact]
    public async Task GetHomeTimeline_AuthFails_ReturnsJsonError()
    {
        Environment.SetEnvironmentVariable("TWITTER_AUTH_TOKEN", null);
        Environment.SetEnvironmentVariable("TWITTER_CT0", null);

        var tools = new McpTools();
        var result = await tools.GetHomeTimeline(20);
        var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    // ── McpTools.GetUserTimeline error tests ──────────────────────────────

    [Fact]
    public async Task GetUserTimeline_AuthFails_ReturnsJsonError()
    {
        Environment.SetEnvironmentVariable("TWITTER_AUTH_TOKEN", null);
        Environment.SetEnvironmentVariable("TWITTER_CT0", null);

        var tools = new McpTools();
        var result = await tools.GetUserTimeline("testuser", 20);
        var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }
}
