// Tests for Auth - mirrors test_auth.py
using XCliSharp;
using Xunit;

namespace XCliSharp.Tests;

public class AuthTests
{
    [Fact]
    public void LoadFromEnv_BothSet_ReturnsCreds()
    {
        Environment.SetEnvironmentVariable("TWITTER_AUTH_TOKEN", "test_auth_token");
        Environment.SetEnvironmentVariable("TWITTER_CT0", "test_ct0");

        try
        {
            var creds = Auth.LoadFromEnv();
            Assert.NotNull(creds);
            Assert.Equal("test_auth_token", creds!.AuthToken);
            Assert.Equal("test_ct0", creds.Ct0);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TWITTER_AUTH_TOKEN", null);
            Environment.SetEnvironmentVariable("TWITTER_CT0", null);
        }
    }

    [Fact]
    public void LoadFromEnv_OnlyAuthToken_ReturnsNull()
    {
        Environment.SetEnvironmentVariable("TWITTER_AUTH_TOKEN", "test_auth_token");
        Environment.SetEnvironmentVariable("TWITTER_CT0", null);

        try
        {
            var creds = Auth.LoadFromEnv();
            Assert.Null(creds);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TWITTER_AUTH_TOKEN", null);
        }
    }

    [Fact]
    public void LoadFromEnv_Neither_ReturnsNull()
    {
        Environment.SetEnvironmentVariable("TWITTER_AUTH_TOKEN", null);
        Environment.SetEnvironmentVariable("TWITTER_CT0", null);

        var creds = Auth.LoadFromEnv();
        Assert.Null(creds);
    }

    [Fact]
    public void Resolve_NoCreds_ThrowsAuthenticationException()
    {
        Environment.SetEnvironmentVariable("TWITTER_AUTH_TOKEN", null);
        Environment.SetEnvironmentVariable("TWITTER_CT0", null);

        Assert.Throws<AuthenticationException>(() => Auth.Resolve());
    }

    [Fact]
    public void Resolve_WithCreds_ReturnsCreds()
    {
        Environment.SetEnvironmentVariable("TWITTER_AUTH_TOKEN", "auth_token_value");
        Environment.SetEnvironmentVariable("TWITTER_CT0", "ct0_value");

        try
        {
            var creds = Auth.Resolve();
            Assert.Equal("auth_token_value", creds.AuthToken);
            Assert.Equal("ct0_value", creds.Ct0);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TWITTER_AUTH_TOKEN", null);
            Environment.SetEnvironmentVariable("TWITTER_CT0", null);
        }
    }
}
