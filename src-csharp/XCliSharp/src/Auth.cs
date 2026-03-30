// Authentication for XCliSharp.
// Mirrors twitter_cli/auth.py (env-var based, no browser cookie extraction)

namespace XCliSharp;

public record AuthCredentials(
    string AuthToken,
    string Ct0,
    string? CookieString = null
);

public static class Auth
{
    /// <summary>Load credentials from environment variables TWITTER_AUTH_TOKEN + TWITTER_CT0.</summary>
    public static AuthCredentials? LoadFromEnv()
    {
        var authToken = Environment.GetEnvironmentVariable("TWITTER_AUTH_TOKEN") ?? "";
        var ct0 = Environment.GetEnvironmentVariable("TWITTER_CT0") ?? "";
        if (!string.IsNullOrEmpty(authToken) && !string.IsNullOrEmpty(ct0))
            return new AuthCredentials(authToken, ct0);
        return null;
    }

    /// <summary>
    /// Resolve credentials: try env vars first, then throw AuthenticationException with guidance.
    /// </summary>
    public static AuthCredentials Resolve()
    {
        var creds = LoadFromEnv();
        if (creds is not null) return creds;

        throw new AuthenticationException(
            "Twitter credentials not found.\n" +
            "Set environment variables:\n" +
            "  TWITTER_AUTH_TOKEN=<your auth_token cookie>\n" +
            "  TWITTER_CT0=<your ct0 cookie>\n\n" +
            "You can find these values in your browser's developer tools:\n" +
            "  1. Open x.com in your browser\n" +
            "  2. Open DevTools → Application → Cookies → https://x.com\n" +
            "  3. Copy the values for 'auth_token' and 'ct0'"
        );
    }
}
