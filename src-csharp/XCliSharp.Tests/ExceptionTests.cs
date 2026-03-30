// Tests for Exceptions - verifies error codes and hierarchy
using XCliSharp;
using Xunit;

namespace XCliSharp.Tests;

public class ExceptionTests
{
    [Fact]
    public void TwitterException_IsRuntimeException()
    {
        var ex = new TwitterException("test");
        Assert.IsAssignableFrom<Exception>(ex);
        Assert.Equal("api_error", ex.ErrorCode);
    }

    [Fact]
    public void AuthenticationException_ErrorCode_IsNotAuthenticated()
    {
        var ex = new AuthenticationException("bad cookies");
        Assert.Equal("not_authenticated", ex.ErrorCode);
        Assert.IsAssignableFrom<TwitterException>(ex);
    }

    [Fact]
    public void RateLimitException_ErrorCode_IsRateLimited()
    {
        var ex = new RateLimitException("too many requests");
        Assert.Equal("rate_limited", ex.ErrorCode);
    }

    [Fact]
    public void NotFoundException_ErrorCode_IsNotFound()
    {
        var ex = new NotFoundException("not found");
        Assert.Equal("not_found", ex.ErrorCode);
    }

    [Fact]
    public void NetworkException_ErrorCode_IsNetworkError()
    {
        var ex = new NetworkException("connection refused");
        Assert.Equal("network_error", ex.ErrorCode);
    }

    [Fact]
    public void InvalidInputException_ErrorCode_IsInvalidInput()
    {
        var ex = new InvalidInputException("bad input");
        Assert.Equal("invalid_input", ex.ErrorCode);
    }

    [Fact]
    public void TwitterApiException_401_IsNotAuthenticated()
    {
        var ex = new TwitterApiException(401, "Unauthorized");
        Assert.Equal("not_authenticated", ex.ErrorCode);
        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public void TwitterApiException_403_IsNotAuthenticated()
    {
        var ex = new TwitterApiException(403, "Forbidden");
        Assert.Equal("not_authenticated", ex.ErrorCode);
    }

    [Fact]
    public void TwitterApiException_429_IsRateLimited()
    {
        var ex = new TwitterApiException(429, "Too Many Requests");
        Assert.Equal("rate_limited", ex.ErrorCode);
    }

    [Fact]
    public void TwitterApiException_404_IsNotFound()
    {
        var ex = new TwitterApiException(404, "Not Found");
        Assert.Equal("not_found", ex.ErrorCode);
    }

    [Fact]
    public void TwitterApiException_500_IsApiError()
    {
        var ex = new TwitterApiException(500, "Internal Server Error");
        Assert.Equal("api_error", ex.ErrorCode);
    }

    [Fact]
    public void TwitterApiException_MessageContainsStatusCode()
    {
        var ex = new TwitterApiException(404, "Not Found");
        Assert.Contains("404", ex.Message);
        Assert.Contains("Not Found", ex.Message);
    }
}
